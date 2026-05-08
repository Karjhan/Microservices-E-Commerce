from app.core.logging import get_logger
from app.domain.retrieval_result import RetrievalResult
from app.graph.graph_repository import GraphRepository

logger = get_logger(__name__)


class GraphExpansionStrategy:
    def __init__(self, graph_repo: GraphRepository) -> None:
        self._graph_repo = graph_repo

    async def expand(
        self,
        candidates: list[RetrievalResult],
        limit: int = 20,
    ) -> list[RetrievalResult]:
        if not candidates:
            return []

        product_ids = [c.product_id for c in candidates[:10]] 

        connectivity = await self._graph_repo.score_candidates(product_ids)
        for candidate in candidates:
            score = connectivity.get(candidate.product_id, 0.0)
            if (score > 0.0):
                candidate.graph_score = score

        graph_results = await self._graph_repo.expand_products(
            product_ids=product_ids, limit=limit
        )

        expanded = []
        existing_ids = {c.product_id for c in candidates}

        for item in graph_results:
            if item["product_id"] not in existing_ids:
                expanded.append(
                    RetrievalResult(
                        product_id=item["product_id"],
                        name=item.get("name", ""),
                        slug=item.get("slug", ""),
                        graph_score=0.5,  
                        metadata={
                            "average_rating": item.get("average_rating", 0),
                            "download_count": item.get("download_count", 0),
                        },
                        source="neo4j",
                    )
                )

        logger.info("Graph expansion completed", expanded=len(expanded))
        return expanded
