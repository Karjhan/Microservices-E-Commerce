from unittest.mock import AsyncMock
from uuid import uuid4

import pytest

from app.application.template_renderer import TemplateRenderer
from app.application.notification_dispatcher import NotificationDispatcher
from app.delivery.base import TransientDeliveryError
from app.domain.enums import DeliveryStatus, NotificationType


@pytest.mark.asyncio
async def test_dispatcher_routes_to_dlq_after_retry_exhaustion(db_session, templates_dir):
    channel = AsyncMock()
    channel.send = AsyncMock(side_effect=TransientDeliveryError("smtp down"))

    dispatcher = NotificationDispatcher(
        channel=channel,
        renderer=TemplateRenderer(templates_dir),
    )
    dlx = AsyncMock()
    dispatcher.set_dlq_channel(AsyncMock(), dlx)

    user_id = str(uuid4())
    await dispatcher.dispatch(
        db_session,
        routing_key="user.registered",
        entity_id=user_id,
        recipient="test@example.com",
        notification_type=NotificationType.WELCOME,
        template_slug="welcome",
        template_context={"userId": user_id, "email": "test@example.com"},
    )

    assert channel.send.await_count == 4
    dlx.publish.assert_awaited_once()

    from app.persistence.repositories import NotificationRepository

    repo = NotificationRepository()
    notification = await repo.find_by_idempotency_key(
        db_session, f"user.registered:{user_id}"
    )
    assert notification is not None
    assert notification.status == DeliveryStatus.DEAD_LETTERED.value
