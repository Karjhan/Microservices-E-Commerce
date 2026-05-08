from app.core.logging import get_logger
from app.ingestion.transformers import transform_product_updated
from app.vector.vector_store import VectorStore
from app.graph.graph_repository import GraphRepository

logger = get_logger(__name__)


class ProductUpdatedHandler:
    def __init__(self, vector_store: VectorStore, graph_repo: GraphRepository) -> None:
        self._vector_store = vector_store
        self._graph_repo = graph_repo

    async def handle(self, payload: dict) -> None:
        product_id = payload.get("ProductId", "unknown")
        logger.info("Handling ProductUpdated", product_id=product_id)

        document = transform_product_updated(payload)

        await self._vector_store.upsert_product(document)
        await self._graph_repo.upsert_product(document)

        logger.info("ProductUpdated processed", product_id=product_id)
