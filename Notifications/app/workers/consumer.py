import asyncio
import json
import uuid

import structlog
from aio_pika import ExchangeType, IncomingMessage, connect_robust

from app.contracts.normalize import normalize_wire_payload
from app.application.notification_dispatcher import NotificationDispatcher
from app.core.logging import configure_logging, get_logger
from app.core.settings import get_settings
from app.handlers.inventory_depleted_handler import InventoryDepletedHandler
from app.handlers.order_cancelled_handler import OrderCancelledHandler
from app.handlers.order_created_handler import OrderCreatedHandler
from app.handlers.order_delivered_handler import OrderDeliveredHandler
from app.handlers.order_payment_failed_handler import OrderPaymentFailedHandler
from app.handlers.order_shipped_handler import OrderShippedHandler
from app.handlers.payment_cancelled_handler import PaymentCancelledHandler
from app.handlers.payment_completed_handler import PaymentCompletedHandler
from app.handlers.payment_failed_handler import PaymentFailedHandler
from app.handlers.payment_refunded_handler import PaymentRefundedHandler
from app.handlers.user_registered_handler import UserRegisteredHandler
from app.persistence.database import get_session_factory

logger = get_logger(__name__)

EXCHANGE_BINDINGS: dict[str, list[str]] = {
    "ecommerce.events": ["user.registered"],
    "orders-exchange": [
        "order.created",
        "order.cancelled",
        "order.shipped",
        "order.delivered",
        "order.payment.failed",
    ],
    "payments-exchange": [
        "payment.completed",
        "payment.failed",
        "payment.cancelled",
        "payment.refunded",
    ],
    "inventory-exchange": ["inventory.depleted"],
}


def _extract_correlation_id(message: IncomingMessage) -> str | None:
    headers = message.headers or {}
    for key in ("X-Correlation-Id", "x-correlation-id", "correlationId"):
        if key in headers:
            return str(headers[key])
    return None


class EventConsumer:
    def __init__(self) -> None:
        self._settings = get_settings()
        self._session_factory = get_session_factory()
        self._dispatcher = NotificationDispatcher()

        self._handlers = {
            UserRegisteredHandler.ROUTING_KEY: UserRegisteredHandler(self._dispatcher),
            OrderCreatedHandler.ROUTING_KEY: OrderCreatedHandler(self._dispatcher),
            OrderCancelledHandler.ROUTING_KEY: OrderCancelledHandler(self._dispatcher),
            OrderShippedHandler.ROUTING_KEY: OrderShippedHandler(self._dispatcher),
            OrderDeliveredHandler.ROUTING_KEY: OrderDeliveredHandler(self._dispatcher),
            OrderPaymentFailedHandler.ROUTING_KEY: OrderPaymentFailedHandler(self._dispatcher),
            PaymentCompletedHandler.ROUTING_KEY: PaymentCompletedHandler(self._dispatcher),
            PaymentFailedHandler.ROUTING_KEY: PaymentFailedHandler(self._dispatcher),
            PaymentCancelledHandler.ROUTING_KEY: PaymentCancelledHandler(self._dispatcher),
            PaymentRefundedHandler.ROUTING_KEY: PaymentRefundedHandler(self._dispatcher),
            InventoryDepletedHandler.ROUTING_KEY: InventoryDepletedHandler(self._dispatcher),
        }

    async def start(self) -> None:
        logger.info("Connecting to RabbitMQ", url=self._settings.rabbitmq_url)
        connection = await connect_robust(self._settings.rabbitmq_url)
        channel = await connection.channel()
        await channel.set_qos(prefetch_count=10)

        dlx = await channel.declare_exchange(
            self._settings.rabbitmq_dlx,
            ExchangeType.DIRECT,
            durable=True,
        )
        dlq = await channel.declare_queue(self._settings.rabbitmq_dlq, durable=True)
        await dlq.bind(dlx, routing_key=self._settings.rabbitmq_dlq)
        self._dispatcher.set_dlq_channel(channel, dlx)

        queue = await channel.declare_queue(
            self._settings.rabbitmq_queue,
            durable=True,
            arguments={
                "x-dead-letter-exchange": self._settings.rabbitmq_dlx,
                "x-dead-letter-routing-key": self._settings.rabbitmq_dlq,
            },
        )

        for exchange_name, routing_keys in EXCHANGE_BINDINGS.items():
            exchange = await channel.declare_exchange(
                exchange_name, ExchangeType.TOPIC, durable=True
            )
            for routing_key in routing_keys:
                await queue.bind(exchange, routing_key)
                logger.info("Bound queue to routing key", exchange=exchange_name, routing_key=routing_key)

        logger.info("Consumer started, waiting for events...")
        await queue.consume(self._on_message)

        try:
            await asyncio.Future()
        finally:
            await connection.close()

    async def _on_message(self, message: IncomingMessage) -> None:
        routing_key = message.routing_key or ""
        correlation_id = _extract_correlation_id(message)

        if correlation_id:
            structlog.contextvars.bind_contextvars(correlation_id=correlation_id)
        else:
            structlog.contextvars.bind_contextvars(correlation_id=str(uuid.uuid4()))

        try:
            handler = self._handlers.get(routing_key)
            if handler is None:
                logger.warning("Unknown routing key", routing_key=routing_key)
                await message.ack()
                return

            try:
                raw_payload = json.loads(message.body.decode("utf-8"))
            except json.JSONDecodeError as exc:
                logger.error("Malformed JSON payload", routing_key=routing_key, error=str(exc))
                await message.nack(requeue=False)
                return

            payload = normalize_wire_payload(raw_payload)

            logger.info("Received event", routing_key=routing_key)

            async with self._session_factory() as session:
                await handler.handle(session, payload, correlation_id)

            await message.ack()
        except Exception as exc:
            logger.error(
                "Error processing event",
                routing_key=routing_key,
                error=str(exc),
                exc_info=True,
            )
            await message.nack(requeue=True)
        finally:
            structlog.contextvars.unbind_contextvars("correlation_id")


async def main() -> None:
    configure_logging()
    from app.core.telemetry import configure_telemetry

    configure_telemetry(None)
    logger.info("Initializing Notifications event consumer")
    consumer = EventConsumer()
    await consumer.start()


if __name__ == "__main__":
    asyncio.run(main())
