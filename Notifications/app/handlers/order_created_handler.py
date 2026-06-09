from sqlalchemy.ext.asyncio import AsyncSession

from app.application.notification_dispatcher import NotificationDispatcher
from app.contracts.events import OrderCreatedEvent
from app.domain.enums import NotificationType


def _format_line_summary(event: OrderCreatedEvent) -> str:
    if event.line_summary:
        return event.line_summary
    if not event.items:
        return ""
    return ", ".join(f"{item.quantity}× {item.product_id}" for item in event.items)


class OrderCreatedHandler:
    ROUTING_KEY = "order.created"

    def __init__(self, dispatcher: NotificationDispatcher) -> None:
        self._dispatcher = dispatcher

    async def handle(
        self, session: AsyncSession, payload: dict, correlation_id: str | None
    ) -> None:
        event = OrderCreatedEvent.model_validate(payload)
        if not event.recipient_email:
            await self._dispatcher.skip_invalid_payload(
                session,
                routing_key=self.ROUTING_KEY,
                entity_id=str(event.order_id),
                recipient=None,
                notification_type=NotificationType.ORDER_CONFIRMATION,
                reason="Missing recipientEmail on order.created",
                correlation_id=correlation_id,
            )
            return

        await self._dispatcher.dispatch(
            session,
            routing_key=self.ROUTING_KEY,
            entity_id=str(event.order_id),
            recipient=event.recipient_email,
            notification_type=NotificationType.ORDER_CONFIRMATION,
            template_slug="order_confirmation",
            template_context={
                "orderId": str(event.order_id),
                "lineSummary": _format_line_summary(event),
                "totalAmount": event.total_amount,
                "currency": event.currency,
            },
            correlation_id=correlation_id,
        )
