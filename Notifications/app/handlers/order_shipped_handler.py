from sqlalchemy.ext.asyncio import AsyncSession

from app.application.notification_dispatcher import NotificationDispatcher
from app.contracts.events import OrderShippedEvent
from app.domain.enums import NotificationType


class OrderShippedHandler:
    ROUTING_KEY = "order.shipped"

    def __init__(self, dispatcher: NotificationDispatcher) -> None:
        self._dispatcher = dispatcher

    async def handle(
        self, session: AsyncSession, payload: dict, correlation_id: str | None
    ) -> None:
        event = OrderShippedEvent.model_validate(payload)
        if not event.recipient_email:
            await self._dispatcher.skip_invalid_payload(
                session,
                routing_key=self.ROUTING_KEY,
                entity_id=str(event.order_id),
                recipient=None,
                notification_type=NotificationType.SHIPPING_UPDATE,
                reason="Missing recipientEmail on order.shipped",
                correlation_id=correlation_id,
            )
            return

        await self._dispatcher.dispatch(
            session,
            routing_key=self.ROUTING_KEY,
            entity_id=str(event.order_id),
            recipient=event.recipient_email,
            notification_type=NotificationType.SHIPPING_UPDATE,
            template_slug="shipping_update",
            template_context={
                "orderId": str(event.order_id),
                "shippedAt": event.shipped_at.isoformat() if event.shipped_at else "",
            },
            correlation_id=correlation_id,
        )
