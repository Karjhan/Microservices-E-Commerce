from sqlalchemy.ext.asyncio import AsyncSession

from app.application.notification_dispatcher import NotificationDispatcher
from app.contracts.events import PaymentCompletedEvent
from app.domain.enums import NotificationType


class PaymentCompletedHandler:
    ROUTING_KEY = "payment.completed"

    def __init__(self, dispatcher: NotificationDispatcher) -> None:
        self._dispatcher = dispatcher

    async def handle(
        self, session: AsyncSession, payload: dict, correlation_id: str | None
    ) -> None:
        event = PaymentCompletedEvent.model_validate(payload)
        if not event.recipient_email:
            await self._dispatcher.skip_invalid_payload(
                session,
                routing_key=self.ROUTING_KEY,
                entity_id=str(event.payment_id),
                recipient=None,
                notification_type=NotificationType.PAYMENT_RECEIPT,
                reason="Missing recipientEmail on payment.completed",
                correlation_id=correlation_id,
            )
            return

        await self._dispatcher.dispatch(
            session,
            routing_key=self.ROUTING_KEY,
            entity_id=str(event.payment_id),
            recipient=event.recipient_email,
            notification_type=NotificationType.PAYMENT_RECEIPT,
            template_slug="payment_receipt",
            template_context={
                "paymentId": str(event.payment_id),
                "orderId": str(event.order_id),
                "amount": event.amount,
                "currency": event.currency,
                "completedAt": event.completed_at.isoformat() if event.completed_at else "",
            },
            correlation_id=correlation_id,
        )
