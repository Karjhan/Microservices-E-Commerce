from unittest.mock import AsyncMock
from uuid import uuid4

import pytest

from app.application.notification_dispatcher import NotificationDispatcher
from app.application.template_renderer import TemplateRenderer
from app.handlers.order_created_handler import OrderCreatedHandler
from app.handlers.user_registered_handler import UserRegisteredHandler


@pytest.mark.asyncio
async def test_user_registered_handler_skips_missing_email(db_session, templates_dir):
    dispatcher = AsyncMock(spec=NotificationDispatcher)
    handler = UserRegisteredHandler(dispatcher)

    await handler.handle(
        db_session,
        {"userId": str(uuid4()), "email": ""},
        correlation_id="corr-1",
    )

    dispatcher.skip_invalid_payload.assert_awaited_once()


@pytest.mark.asyncio
async def test_order_created_handler_formats_line_summary(db_session, templates_dir):
    dispatcher = AsyncMock(spec=NotificationDispatcher)
    handler = OrderCreatedHandler(dispatcher)
    order_id = uuid4()

    await handler.handle(
        db_session,
        {
            "orderId": str(order_id),
            "userId": str(uuid4()),
            "totalAmount": 49.99,
            "currency": "EUR",
            "itemCount": 1,
            "recipientEmail": "buyer@example.com",
            "items": [{"productId": str(uuid4()), "quantity": 2}],
        },
        correlation_id="corr-2",
    )

    dispatcher.dispatch.assert_awaited_once()
    kwargs = dispatcher.dispatch.await_args.kwargs
    assert kwargs["template_context"]["lineSummary"].startswith("2×")
