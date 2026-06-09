from sqlalchemy.ext.asyncio import AsyncSession

from app.application.notification_dispatcher import NotificationDispatcher
from app.contracts.events import OrderDeliveredEvent
from app.domain.enums import NotificationType


class OrderDeliveredHandler:
    ROUTING_KEY = "order.delivered"

    def __init__(self, dispatcher: NotificationDispatcher) -> None:
        self._dispatcher = dispatcher

    async def handle(
        self, session: AsyncSession, payload: dict, correlation_id: str | None
    ) -> None:
        event = OrderDeliveredEvent.model_validate(payload)
        if not event.recipient_email:
            await self._dispatcher.skip_invalid_payload(
                session,
                routing_key=self.ROUTING_KEY,
                entity_id=str(event.order_id),
                recipient=None,
                notification_type=NotificationType.DELIVERY_CONFIRMATION,
                reason="Missing recipientEmail on order.delivered",
                correlation_id=correlation_id,
            )
            return

        await self._dispatcher.dispatch(
            session,
            routing_key=self.ROUTING_KEY,
            entity_id=str(event.order_id),
            recipient=event.recipient_email,
            notification_type=NotificationType.DELIVERY_CONFIRMATION,
            template_slug="delivery_confirmation",
            template_context={
                "orderId": str(event.order_id),
                "deliveredAt": event.delivered_at.isoformat() if event.delivered_at else "",
            },
            correlation_id=correlation_id,
        )
