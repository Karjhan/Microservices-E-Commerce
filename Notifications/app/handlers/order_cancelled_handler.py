from sqlalchemy.ext.asyncio import AsyncSession

from app.application.notification_dispatcher import NotificationDispatcher
from app.contracts.events import OrderCancelledEvent
from app.domain.enums import NotificationType


class OrderCancelledHandler:
    ROUTING_KEY = "order.cancelled"

    def __init__(self, dispatcher: NotificationDispatcher) -> None:
        self._dispatcher = dispatcher

    async def handle(
        self, session: AsyncSession, payload: dict, correlation_id: str | None
    ) -> None:
        event = OrderCancelledEvent.model_validate(payload)
        if not event.recipient_email:
            await self._dispatcher.skip_invalid_payload(
                session,
                routing_key=self.ROUTING_KEY,
                entity_id=str(event.order_id),
                recipient=None,
                notification_type=NotificationType.ORDER_CANCELLATION,
                reason="Missing recipientEmail on order.cancelled",
                correlation_id=correlation_id,
            )
            return

        await self._dispatcher.dispatch(
            session,
            routing_key=self.ROUTING_KEY,
            entity_id=str(event.order_id),
            recipient=event.recipient_email,
            notification_type=NotificationType.ORDER_CANCELLATION,
            template_slug="order_cancellation",
            template_context={
                "orderId": str(event.order_id),
                "reason": event.reason or "",
            },
            correlation_id=correlation_id,
        )
