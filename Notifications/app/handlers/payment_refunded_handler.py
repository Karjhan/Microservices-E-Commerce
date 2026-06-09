from sqlalchemy.ext.asyncio import AsyncSession

from app.application.notification_dispatcher import NotificationDispatcher
from app.contracts.events import PaymentRefundedEvent
from app.domain.enums import NotificationType


class PaymentRefundedHandler:
    ROUTING_KEY = "payment.refunded"

    def __init__(self, dispatcher: NotificationDispatcher) -> None:
        self._dispatcher = dispatcher

    async def handle(
        self, session: AsyncSession, payload: dict, correlation_id: str | None
    ) -> None:
        event = PaymentRefundedEvent.model_validate(payload)
        if not event.recipient_email:
            await self._dispatcher.skip_invalid_payload(
                session,
                routing_key=self.ROUTING_KEY,
                entity_id=str(event.payment_id),
                recipient=None,
                notification_type=NotificationType.REFUND_CONFIRMATION,
                reason="Missing recipientEmail on payment.refunded",
                correlation_id=correlation_id,
            )
            return

        await self._dispatcher.dispatch(
            session,
            routing_key=self.ROUTING_KEY,
            entity_id=str(event.payment_id),
            recipient=event.recipient_email,
            notification_type=NotificationType.REFUND_CONFIRMATION,
            template_slug="refund_confirmation",
            template_context={
                "paymentId": str(event.payment_id),
                "orderId": str(event.order_id),
                "refundAmount": event.refund_amount,
                "currency": event.currency,
                "isFullRefund": event.is_full_refund,
            },
            correlation_id=correlation_id,
        )
