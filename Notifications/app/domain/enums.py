from enum import Enum


class NotificationType(str, Enum):
    WELCOME = "Welcome"
    ORDER_CONFIRMATION = "OrderConfirmation"
    PAYMENT_RECEIPT = "PaymentReceipt"
    PAYMENT_FAILURE = "PaymentFailure"
    PAYMENT_CANCELLATION = "PaymentCancellation"
    REFUND_CONFIRMATION = "RefundConfirmation"
    ORDER_CANCELLATION = "OrderCancellation"
    CHECKOUT_PAYMENT_FAILURE = "CheckoutPaymentFailure"
    SHIPPING_UPDATE = "ShippingUpdate"
    DELIVERY_CONFIRMATION = "DeliveryConfirmation"
    LOW_STOCK_ALERT = "LowStockAlert"


class DeliveryStatus(str, Enum):
    PENDING = "Pending"
    SENT = "Sent"
    FAILED = "Failed"
    DEAD_LETTERED = "DeadLettered"


class ChannelType(str, Enum):
    EMAIL = "Email"
    SMS = "Sms"
    PUSH = "Push"


class ProcessedOutcome(str, Enum):
    SENT = "Sent"
    FAILED_PERMANENTLY = "FailedPermanently"
    SKIPPED_INVALID_PAYLOAD = "SkippedInvalidPayload"
