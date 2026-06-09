from sqlalchemy.ext.asyncio import AsyncSession

from app.application.notification_dispatcher import NotificationDispatcher
from app.contracts.events import InventoryDepletedEvent
from app.core.settings import get_settings
from app.domain.enums import NotificationType


class InventoryDepletedHandler:
    ROUTING_KEY = "inventory.depleted"

    def __init__(self, dispatcher: NotificationDispatcher) -> None:
        self._dispatcher = dispatcher

    async def handle(
        self, session: AsyncSession, payload: dict, correlation_id: str | None
    ) -> None:
        event = InventoryDepletedEvent.model_validate(payload)
        operators = get_settings().operator_email_list
        if not operators:
            await self._dispatcher.skip_invalid_payload(
                session,
                routing_key=self.ROUTING_KEY,
                entity_id=str(event.product_id),
                recipient=None,
                notification_type=NotificationType.LOW_STOCK_ALERT,
                reason="NOTIFICATIONS_OPERATOR_EMAILS not configured",
                correlation_id=correlation_id,
            )
            return

        context = {
            "productId": str(event.product_id),
            "depletedAt": event.depleted_at.isoformat() if event.depleted_at else "",
        }

        await self._dispatcher.dispatch_to_recipients(
            session,
            routing_key=self.ROUTING_KEY,
            entity_id=str(event.product_id),
            recipients=operators,
            notification_type=NotificationType.LOW_STOCK_ALERT,
            template_slug="low_stock_alert",
            template_context=context,
            correlation_id=correlation_id,
        )
