from sqlalchemy.exc import IntegrityError
from sqlalchemy.ext.asyncio import AsyncSession

from app.domain.enums import ProcessedOutcome
from app.persistence.models import ProcessedEventLog
from app.persistence.repositories import ProcessedEventLogRepository, utc_now


class IdempotencyService:
    def __init__(self, repo: ProcessedEventLogRepository | None = None) -> None:
        self._repo = repo or ProcessedEventLogRepository()

    @staticmethod
    def build_key(routing_key: str, entity_id: str) -> str:
        return f"{routing_key}:{entity_id}"

    async def is_processed(self, session: AsyncSession, idempotency_key: str) -> bool:
        return await self._repo.exists(session, idempotency_key)

    async def mark_processed(
        self,
        session: AsyncSession,
        *,
        idempotency_key: str,
        event_type: str,
        outcome: ProcessedOutcome,
    ) -> bool:
        log = ProcessedEventLog(
            idempotency_key=idempotency_key,
            event_type=event_type,
            processed_at=utc_now(),
            outcome=outcome.value,
        )
        try:
            await self._repo.create(session, log)
            return True
        except IntegrityError:
            await session.rollback()
            return False
