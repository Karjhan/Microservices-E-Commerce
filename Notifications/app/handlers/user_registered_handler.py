from sqlalchemy.ext.asyncio import AsyncSession

from app.application.notification_dispatcher import NotificationDispatcher
from app.contracts.events import UserRegisteredEvent
from app.domain.enums import NotificationType


class UserRegisteredHandler:
    ROUTING_KEY = "user.registered"

    def __init__(self, dispatcher: NotificationDispatcher) -> None:
        self._dispatcher = dispatcher

    async def handle(
        self, session: AsyncSession, payload: dict, correlation_id: str | None
    ) -> None:
        event = UserRegisteredEvent.model_validate(payload)
        if not event.email:
            await self._dispatcher.skip_invalid_payload(
                session,
                routing_key=self.ROUTING_KEY,
                entity_id=str(event.user_id),
                recipient=None,
                notification_type=NotificationType.WELCOME,
                reason="Missing email on user.registered",
                correlation_id=correlation_id,
            )
            return

        await self._dispatcher.dispatch(
            session,
            routing_key=self.ROUTING_KEY,
            entity_id=str(event.user_id),
            recipient=event.email,
            notification_type=NotificationType.WELCOME,
            template_slug="welcome",
            template_context={"userId": str(event.user_id), "email": event.email},
            correlation_id=correlation_id,
        )
