from datetime import datetime
from uuid import UUID

from pydantic import BaseModel, computed_field

from app.domain.enums import DeliveryStatus, NotificationType, ChannelType


class HealthResponse(BaseModel):
    status: str
    postgres: str
    rabbitmq: str
    smtp: str


class NotificationResponse(BaseModel):
    id: UUID
    idempotencyKey: str
    eventType: str
    notificationType: NotificationType
    channel: ChannelType
    recipient: str
    subject: str
    bodyPreview: str | None = None
    status: DeliveryStatus
    attemptCount: int
    correlationId: str | None = None
    errorDetails: str | None = None
    createdAt: datetime
    sentAt: datetime | None = None
    updatedAt: datetime

    model_config = {"from_attributes": True}


class NotificationListResponse(BaseModel):
    items: list[NotificationResponse]
    page: int
    pageSize: int
    totalCount: int

    @computed_field  # type: ignore[prop-decorator]
    @property
    def totalPages(self) -> int:
        if self.pageSize <= 0:
            return 0
        return (self.totalCount + self.pageSize - 1) // self.pageSize
