import uuid
from datetime import datetime, timezone

from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.domain.enums import DeliveryStatus
from app.persistence.models import Notification, ProcessedEventLog


class NotificationRepository:
    async def create(self, session: AsyncSession, notification: Notification) -> Notification:
        session.add(notification)
        await session.flush()
        return notification

    async def get_by_id(self, session: AsyncSession, notification_id: uuid.UUID) -> Notification | None:
        return await session.get(Notification, notification_id)

    async def find_by_idempotency_key(
        self, session: AsyncSession, idempotency_key: str
    ) -> Notification | None:
        result = await session.execute(
            select(Notification).where(Notification.idempotency_key == idempotency_key)
        )
        return result.scalar_one_or_none()

    async def list_notifications(
        self,
        session: AsyncSession,
        *,
        event_id: str | None = None,
        event_type: str | None = None,
        recipient: str | None = None,
        status: DeliveryStatus | None = None,
        page: int = 1,
        page_size: int = 20,
    ) -> tuple[list[Notification], int]:
        query = select(Notification)
        count_query = select(func.count()).select_from(Notification)

        if event_id:
            suffix = f":{event_id}"
            query = query.where(Notification.idempotency_key.endswith(suffix))
            count_query = count_query.where(Notification.idempotency_key.endswith(suffix))
        if event_type:
            query = query.where(Notification.event_type == event_type)
            count_query = count_query.where(Notification.event_type == event_type)
        if recipient:
            query = query.where(Notification.recipient == recipient)
            count_query = count_query.where(Notification.recipient == recipient)
        if status:
            query = query.where(Notification.status == status.value)
            count_query = count_query.where(Notification.status == status.value)

        total = (await session.execute(count_query)).scalar_one()

        offset = (page - 1) * page_size
        query = query.order_by(Notification.created_at.desc()).offset(offset).limit(page_size)
        items = list((await session.execute(query)).scalars().all())
        return items, total


class ProcessedEventLogRepository:
    async def exists(self, session: AsyncSession, idempotency_key: str) -> bool:
        result = await session.execute(
            select(ProcessedEventLog.id).where(
                ProcessedEventLog.idempotency_key == idempotency_key
            )
        )
        return result.scalar_one_or_none() is not None

    async def create(self, session: AsyncSession, log: ProcessedEventLog) -> ProcessedEventLog:
        session.add(log)
        await session.flush()
        return log


def utc_now() -> datetime:
    return datetime.now(timezone.utc)
