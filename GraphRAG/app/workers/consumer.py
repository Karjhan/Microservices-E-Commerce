import asyncio
import json
from aio_pika import connect_robust, IncomingMessage, ExchangeType
from app.core.settings import get_settings
from app.core.logging import configure_logging, get_logger
from app.embeddings.service import get_embedding_provider
from app.vector.vector_store import VectorStore
from app.vector.qdrant_client import ensure_collection
from app.graph.graph_repository import GraphRepository
from app.graph.neo4j_client import ensure_constraints
from app.ingestion.handlers.product_created_handler import ProductCreatedHandler
from app.ingestion.handlers.product_updated_handler import ProductUpdatedHandler
from app.ingestion.handlers.product_deleted_handler import ProductDeletedHandler
from app.ingestion.handlers.relation_handler import (
    RelationAddedHandler,
    RelationDeletedHandler,
)
from app.ingestion.handlers.attribute_handler import (
    AttributeAddedHandler,
    AttributeDeletedHandler,
)

logger = get_logger(__name__)

# Routing key → event type mapping
# Must match the routing keys used by the Catalog service's RabbitMqPublisher
ROUTING_KEY_MAP = {
    "product.created": "ProductCreated",
    "product.updated": "ProductUpdated",
    "product.deleted": "ProductDeleted",
    "product.relation.added": "ProductRelationAdded",
    "product.relation.deleted": "ProductRelationDeleted",
    "product.attribute.added": "ProductAttributeAdded",
    "product.attribute.deleted": "ProductAttributeDeleted",
}


class EventConsumer:
    """RabbitMQ consumer that dispatches catalog events to ingestion handlers."""

    def __init__(self) -> None:
        self._settings = get_settings()

        embedding_provider = get_embedding_provider()
        vector_store = VectorStore(embedding_provider)
        graph_repo = GraphRepository()

        self._handlers = {
            "ProductCreated": ProductCreatedHandler(vector_store, graph_repo),
            "ProductUpdated": ProductUpdatedHandler(vector_store, graph_repo),
            "ProductDeleted": ProductDeletedHandler(vector_store, graph_repo),
            "ProductRelationAdded": RelationAddedHandler(graph_repo),
            "ProductRelationDeleted": RelationDeletedHandler(graph_repo),
            "ProductAttributeAdded": AttributeAddedHandler(vector_store, graph_repo),
            "ProductAttributeDeleted": AttributeDeletedHandler(vector_store, graph_repo),
        }

    async def start(self) -> None:
        """Connect to RabbitMQ and start consuming events."""
        logger.info("Connecting to RabbitMQ", url=self._settings.rabbitmq_url)

        connection = await connect_robust(self._settings.rabbitmq_url)
        channel = await connection.channel()

        await channel.set_qos(prefetch_count=10)

        exchange = await channel.declare_exchange(
            self._settings.rabbitmq_exchange,
            ExchangeType.TOPIC,
            durable=True,
        )

        queue = await channel.declare_queue(
            self._settings.rabbitmq_queue,
            durable=True,
        )

        for routing_key in ROUTING_KEY_MAP:
            await queue.bind(exchange, routing_key)
            logger.info("Bound to routing key", routing_key=routing_key)

        logger.info("Consumer started, waiting for events...")
        await queue.consume(self._on_message)

        try:
            await asyncio.Future()
        finally:
            await connection.close()

    async def _on_message(self, message: IncomingMessage) -> None:
        """Process an incoming RabbitMQ message."""
        async with message.process():
            try:
                routing_key = message.routing_key
                event_type = ROUTING_KEY_MAP.get(routing_key)

                if event_type is None:
                    logger.warning("Unknown routing key", routing_key=routing_key)
                    return

                payload = json.loads(message.body.decode("utf-8"))
                logger.info(
                    "Received event",
                    event_type=event_type,
                    routing_key=routing_key,
                )

                handler = self._handlers.get(event_type)
                if handler:
                    await handler.handle(payload)
                else:
                    logger.warning("No handler for event type", event_type=event_type)

            except json.JSONDecodeError as e:
                logger.error("Failed to decode message body", error=str(e))
            except Exception as e:
                logger.error(
                    "Error processing event",
                    error=str(e),
                    routing_key=message.routing_key,
                    exc_info=True,
                )


async def main() -> None:
    """Entry point for the consumer worker."""
    configure_logging()
    logger.info("Initializing GraphRAG event consumer")

    await ensure_collection()
    await ensure_constraints()

    consumer = EventConsumer()
    await consumer.start()


if __name__ == "__main__":
    asyncio.run(main())
