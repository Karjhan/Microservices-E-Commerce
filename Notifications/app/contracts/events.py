from datetime import datetime
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class EventBase(BaseModel):
    model_config = ConfigDict(populate_by_name=True)


class UserRegisteredEvent(EventBase):
    user_id: UUID = Field(alias="userId")
    email: str
    role: str | None = None


class OrderLineItem(EventBase):
    product_id: UUID = Field(alias="productId")
    quantity: int


class OrderCreatedEvent(EventBase):
    order_id: UUID = Field(alias="orderId")
    user_id: UUID = Field(alias="userId")
    total_amount: float = Field(alias="totalAmount")
    currency: str
    item_count: int = Field(alias="itemCount")
    created_at: datetime | None = Field(default=None, alias="createdAt")
    items: list[OrderLineItem] = Field(default_factory=list)
    recipient_email: str | None = Field(default=None, alias="recipientEmail")
    line_summary: str | None = Field(default=None, alias="lineSummary")


class OrderCancelledEvent(EventBase):
    order_id: UUID = Field(alias="orderId")
    user_id: UUID = Field(alias="userId")
    reason: str | None = None
    cancelled_at: datetime | None = Field(default=None, alias="cancelledAt")
    recipient_email: str | None = Field(default=None, alias="recipientEmail")


class OrderShippedEvent(EventBase):
    order_id: UUID = Field(alias="orderId")
    user_id: UUID = Field(alias="userId")
    shipped_at: datetime | None = Field(default=None, alias="shippedAt")
    recipient_email: str | None = Field(default=None, alias="recipientEmail")


class OrderDeliveredEvent(EventBase):
    order_id: UUID = Field(alias="orderId")
    user_id: UUID = Field(alias="userId")
    delivered_at: datetime | None = Field(default=None, alias="deliveredAt")
    recipient_email: str | None = Field(default=None, alias="recipientEmail")


class OrderPaymentFailedEvent(EventBase):
    order_id: UUID = Field(alias="orderId")
    user_id: UUID = Field(alias="userId")
    reason: str | None = None
    failed_at: datetime | None = Field(default=None, alias="failedAt")
    recipient_email: str | None = Field(default=None, alias="recipientEmail")


class PaymentCompletedEvent(EventBase):
    payment_id: UUID = Field(alias="paymentId")
    order_id: UUID = Field(alias="orderId")
    user_id: UUID = Field(alias="userId")
    amount: float
    currency: str
    provider_payment_id: str | None = Field(default=None, alias="providerPaymentId")
    completed_at: datetime | None = Field(default=None, alias="completedAt")
    recipient_email: str | None = Field(default=None, alias="recipientEmail")


class PaymentFailedEvent(EventBase):
    payment_id: UUID = Field(alias="paymentId")
    order_id: UUID = Field(alias="orderId")
    user_id: UUID = Field(alias="userId")
    amount: float
    currency: str
    reason: str | None = None
    failed_at: datetime | None = Field(default=None, alias="failedAt")
    recipient_email: str | None = Field(default=None, alias="recipientEmail")


class PaymentCancelledEvent(EventBase):
    payment_id: UUID = Field(alias="paymentId")
    order_id: UUID = Field(alias="orderId")
    user_id: UUID = Field(alias="userId")
    reason: str | None = None
    cancelled_at: datetime | None = Field(default=None, alias="cancelledAt")
    recipient_email: str | None = Field(default=None, alias="recipientEmail")


class PaymentRefundedEvent(EventBase):
    payment_id: UUID = Field(alias="paymentId")
    order_id: UUID = Field(alias="orderId")
    user_id: UUID = Field(alias="userId")
    refund_amount: float = Field(alias="refundAmount")
    total_refunded: float = Field(alias="totalRefunded")
    original_amount: float = Field(alias="originalAmount")
    currency: str
    is_full_refund: bool = Field(alias="isFullRefund")
    refunded_at: datetime | None = Field(default=None, alias="refundedAt")
    recipient_email: str | None = Field(default=None, alias="recipientEmail")


class InventoryDepletedEvent(EventBase):
    product_id: UUID = Field(alias="productId")
    depleted_at: datetime | None = Field(default=None, alias="depletedAt")
