from uuid import UUID

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.ext.asyncio import AsyncSession

from app.api.dependencies import get_db_session
from app.api.schemas import NotificationListResponse, NotificationResponse
from app.domain.enums import DeliveryStatus
from app.persistence.repositories import NotificationRepository

router = APIRouter(prefix="/notifications", tags=["Notifications"])
_repo = NotificationRepository()


def _to_response(notification) -> NotificationResponse:
    return NotificationResponse(
        id=notification.id,
        idempotencyKey=notification.idempotency_key,
        eventType=notification.event_type,
        notificationType=notification.notification_type,
        channel=notification.channel,
        recipient=notification.recipient,
        subject=notification.subject,
        bodyPreview=notification.body_preview,
        status=notification.status,
        attemptCount=notification.attempt_count,
        correlationId=notification.correlation_id,
        errorDetails=notification.error_details,
        createdAt=notification.created_at,
        sentAt=notification.sent_at,
        updatedAt=notification.updated_at,
    )


@router.get("", response_model=NotificationListResponse)
async def list_notifications(
    eventId: UUID | None = Query(default=None),
    eventType: str | None = Query(default=None),
    recipient: str | None = Query(default=None),
    status: DeliveryStatus | None = Query(default=None),
    page: int = Query(default=1, ge=1),
    pageSize: int = Query(default=20, ge=1, le=100),
    session: AsyncSession = Depends(get_db_session),
) -> NotificationListResponse:
    items, total = await _repo.list_notifications(
        session,
        event_id=str(eventId) if eventId else None,
        event_type=eventType,
        recipient=recipient,
        status=status,
        page=page,
        page_size=pageSize,
    )
    return NotificationListResponse(
        items=[_to_response(n) for n in items],
        page=page,
        pageSize=pageSize,
        totalCount=total,
    )


@router.get("/{notification_id}", response_model=NotificationResponse)
async def get_notification(
    notification_id: UUID,
    session: AsyncSession = Depends(get_db_session),
) -> NotificationResponse:
    notification = await _repo.get_by_id(session, notification_id)
    if notification is None:
        raise HTTPException(status_code=404, detail="Notification not found")
    return _to_response(notification)
