from __future__ import annotations

from typing import TYPE_CHECKING

from app.core.logging import get_logger

if TYPE_CHECKING:
    from app.vector.vector_store import VectorStore
    from app.graph.graph_repository import GraphRepository

logger = get_logger(__name__)


class ProductStatusChangedHandler:
    """Keeps the search and recommendation indices in step with publish/unpublish (FR-019).

    Search and recommendations only return products whose status is ``Active``; this handler
    updates the stored status without re-embedding the product.
    """

    def __init__(self, vector_store: VectorStore, graph_repo: GraphRepository) -> None:
        self._vector_store = vector_store
        self._graph_repo = graph_repo

    async def handle(self, payload: dict) -> None:
        product_id = payload.get("ProductId", "unknown")
        status = payload.get("Status")
        logger.info("Handling ProductStatusChanged", product_id=product_id, status=status)

        if not status or product_id == "unknown":
            logger.warning("ProductStatusChanged ignored: missing ProductId or Status", payload=payload)
            return

        await self._vector_store.set_status(product_id, status)
        await self._graph_repo.set_status(product_id, status)

        logger.info("ProductStatusChanged processed", product_id=product_id, status=status)
