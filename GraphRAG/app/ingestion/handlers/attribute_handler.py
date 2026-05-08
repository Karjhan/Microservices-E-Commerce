from app.core.logging import get_logger
from app.ingestion.attribute_classifier import classify_attribute
from app.domain.product_document import AttributeBucket
from app.vector.vector_store import VectorStore
from app.graph.graph_repository import GraphRepository

logger = get_logger(__name__)


class AttributeAddedHandler:
    def __init__(self, vector_store: VectorStore, graph_repo: GraphRepository) -> None:
        self._vector_store = vector_store
        self._graph_repo = graph_repo

    async def handle(self, payload: dict) -> None:
        product_id = payload["ProductId"]
        key = payload["Key"]
        value = payload["Value"]
        bucket = classify_attribute(key)

        logger.info(
            "Handling ProductAttributeAdded",
            product_id=product_id,
            key=key,
            bucket=bucket.value,
        )

        await self._graph_repo.add_attribute(product_id, key, value)

        # For Qdrant: we need to re-embed the product to include the new attribute.
        # In a production system, this would fetch the full product state and re-embed.
        # For now, we update the payload only (facet attributes become filterable).
        if bucket == AttributeBucket.FACET:
            from app.vector.qdrant_client import get_qdrant_client, get_settings

            client = get_qdrant_client()
            settings = get_settings()
            client.set_payload(
                collection_name=settings.qdrant_collection,
                payload={f"attr_{key}": value},
                points=[product_id],
            )
            logger.info("Updated Qdrant payload with facet attribute", key=key)


class AttributeDeletedHandler:
    def __init__(self, vector_store: VectorStore, graph_repo: GraphRepository) -> None:
        self._vector_store = vector_store
        self._graph_repo = graph_repo

    async def handle(self, payload: dict) -> None:
        product_id = payload["ProductId"]
        key = payload["Key"]

        logger.info(
            "Handling ProductAttributeDeleted",
            product_id=product_id,
            key=key,
        )

        await self._graph_repo.delete_attribute(product_id, key)

        from app.vector.qdrant_client import get_qdrant_client, get_settings

        client = get_qdrant_client()
        settings = get_settings()
        client.delete_payload(
            collection_name=settings.qdrant_collection,
            keys=[f"attr_{key}"],
            points=[product_id],
        )
        logger.info("Removed attribute from Qdrant payload", key=key)
