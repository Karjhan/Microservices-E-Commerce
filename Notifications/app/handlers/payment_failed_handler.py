from sqlalchemy.ext.asyncio import AsyncSession

from app.application.notification_dispatcher import NotificationDispatcher
from app.contracts.events import PaymentFailedEvent
from app.domain.enums import NotificationType


class PaymentFailedHandler:
    ROUTING_KEY = "payment.failed"

    def __init__(self, dispatcher: NotificationDispatcher) -> None:
        self._dispatcher = dispatcher

    async def handle(
        self, session: AsyncSession, payload: dict, correlation_id: str | None
    ) -> None:
        event = PaymentFailedEvent.model_validate(payload)
        if not event.recipient_email:
            await self._dispatcher.skip_invalid_payload(
                session,
                routing_key=self.ROUTING_KEY,
                entity_id=str(event.payment_id),
                recipient=None,
                notification_type=NotificationType.PAYMENT_FAILURE,
                reason="Missing recipientEmail on payment.failed",
                correlation_id=correlation_id,
            )
            return

        await self._dispatcher.dispatch(
            session,
            routing_key=self.ROUTING_KEY,
            entity_id=str(event.payment_id),
            recipient=event.recipient_email,
            notification_type=NotificationType.PAYMENT_FAILURE,
            template_slug="payment_failure",
            template_context={
                "paymentId": str(event.payment_id),
                "orderId": str(event.order_id),
                "amount": event.amount,
                "currency": event.currency,
                "reason": event.reason or "",
            },
            correlation_id=correlation_id,
        )
