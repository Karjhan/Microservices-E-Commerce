from unittest.mock import AsyncMock
from uuid import uuid4

import pytest

from app.application.notification_dispatcher import NotificationDispatcher
from app.application.template_renderer import TemplateRenderer
from app.delivery.base import TransientDeliveryError
from app.domain.enums import DeliveryStatus, NotificationType
from app.handlers.user_registered_handler import UserRegisteredHandler
from app.persistence.repositories import NotificationRepository


@pytest.mark.asyncio
async def test_retry_exhaustion_marks_dead_lettered(db_session, templates_dir):
    channel = AsyncMock()
    channel.send = AsyncMock(side_effect=TransientDeliveryError("connection refused"))
    dispatcher = NotificationDispatcher(
        channel=channel,
        renderer=TemplateRenderer(templates_dir),
    )
    dlx = AsyncMock()
    dispatcher.set_dlq_channel(AsyncMock(), dlx)
    handler = UserRegisteredHandler(dispatcher)
    user_id = str(uuid4())

    await handler.handle(
        db_session,
        {"userId": user_id, "email": "fail@example.com", "role": "Customer"},
        None,
    )

    repo = NotificationRepository()
    notification = await repo.find_by_idempotency_key(db_session, f"user.registered:{user_id}")
    assert notification is not None
    assert notification.status == DeliveryStatus.DEAD_LETTERED.value
    assert notification.attempt_count == 4
    dlx.publish.assert_awaited_once()
