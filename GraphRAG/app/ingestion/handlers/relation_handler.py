from app.core.logging import get_logger
from app.graph.graph_repository import GraphRepository

logger = get_logger(__name__)


class RelationAddedHandler:
    def __init__(self, graph_repo: GraphRepository) -> None:
        self._graph_repo = graph_repo

    async def handle(self, payload: dict) -> None:
        logger.info(
            "Handling ProductRelationAdded",
            product_id=payload.get("ProductId"),
            related_product_id=payload.get("RelatedProductId"),
        )
        await self._graph_repo.add_relation(
            product_id=payload["ProductId"],
            related_product_id=payload["RelatedProductId"],
            relation_type=payload["Type"],
            relation_id=payload["RelationId"],
        )


class RelationDeletedHandler:
    def __init__(self, graph_repo: GraphRepository) -> None:
        self._graph_repo = graph_repo

    async def handle(self, payload: dict) -> None:
        logger.info(
            "Handling ProductRelationDeleted",
            product_id=payload.get("ProductId"),
            related_product_id=payload.get("RelatedProductId"),
        )
        await self._graph_repo.delete_relation(
            product_id=payload["ProductId"],
            related_product_id=payload["RelatedProductId"],
            relation_id=payload["RelationId"],
        )
