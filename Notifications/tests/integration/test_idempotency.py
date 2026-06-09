from unittest.mock import AsyncMock
from uuid import uuid4

import pytest

from app.application.notification_dispatcher import NotificationDispatcher
from app.application.template_renderer import TemplateRenderer
from app.domain.enums import DeliveryStatus, NotificationType
from app.handlers.user_registered_handler import UserRegisteredHandler
from app.persistence.repositories import NotificationRepository


@pytest.mark.asyncio
async def test_duplicate_event_produces_single_notification_row(db_session, templates_dir):
    channel = AsyncMock()
    dispatcher = NotificationDispatcher(
        channel=channel,
        renderer=TemplateRenderer(templates_dir),
    )
    handler = UserRegisteredHandler(dispatcher)
    user_id = str(uuid4())
    payload = {"userId": user_id, "email": "dup@example.com", "role": "Customer"}

    await handler.handle(db_session, payload, None)
    await handler.handle(db_session, payload, None)

    repo = NotificationRepository()
    notification = await repo.find_by_idempotency_key(db_session, f"user.registered:{user_id}")
    assert notification is not None
    assert notification.status == DeliveryStatus.SENT.value
    channel.send.assert_awaited_once()
