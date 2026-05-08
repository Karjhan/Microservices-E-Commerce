from app.core.logging import get_logger
from app.vector.vector_store import VectorStore
from app.graph.graph_repository import GraphRepository

logger = get_logger(__name__)


class ProductDeletedHandler:
    def __init__(self, vector_store: VectorStore, graph_repo: GraphRepository) -> None:
        self._vector_store = vector_store
        self._graph_repo = graph_repo

    async def handle(self, payload: dict) -> None:
        product_id = payload.get("ProductId", "unknown")
        logger.info("Handling ProductDeleted", product_id=product_id)

        await self._vector_store.delete_product(product_id)
        await self._graph_repo.delete_product(product_id)

        logger.info("ProductDeleted processed", product_id=product_id)
