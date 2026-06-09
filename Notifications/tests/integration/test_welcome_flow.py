from unittest.mock import AsyncMock
from uuid import uuid4

import pytest

from app.application.notification_dispatcher import NotificationDispatcher
from app.application.template_renderer import TemplateRenderer
from app.domain.enums import NotificationType
from app.handlers.user_registered_handler import UserRegisteredHandler


@pytest.mark.asyncio
async def test_welcome_flow_dispatches_once(db_session, templates_dir):
    channel = AsyncMock()
    dispatcher = NotificationDispatcher(
        channel=channel,
        renderer=TemplateRenderer(templates_dir),
    )
    handler = UserRegisteredHandler(dispatcher)
    user_id = str(uuid4())

    await handler.handle(
        db_session,
        {"userId": user_id, "email": "welcome@example.com", "role": "Customer"},
        correlation_id="corr-welcome",
    )

    channel.send.assert_awaited_once()
    assert channel.send.await_args.args[0] == "welcome@example.com"


@pytest.mark.asyncio
async def test_welcome_flow_idempotent_on_redelivery(db_session, templates_dir):
    channel = AsyncMock()
    dispatcher = NotificationDispatcher(
        channel=channel,
        renderer=TemplateRenderer(templates_dir),
    )
    handler = UserRegisteredHandler(dispatcher)
    payload = {"userId": str(uuid4()), "email": "welcome@example.com", "role": "Customer"}

    await handler.handle(db_session, payload, correlation_id="corr-1")
    await handler.handle(db_session, payload, correlation_id="corr-2")

    channel.send.assert_awaited_once()
