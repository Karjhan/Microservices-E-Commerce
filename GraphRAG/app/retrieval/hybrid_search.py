from app.core.logging import get_logger
from app.domain.retrieval_result import RetrievalResult
from app.vector.vector_store import VectorStore

logger = get_logger(__name__)


class HybridSearchStrategy:
    def __init__(self, vector_store: VectorStore) -> None:
        self._vector_store = vector_store

    async def search(
        self,
        query_text: str,
        filters: dict | None = None,
        limit: int = 50,
    ) -> list[RetrievalResult]:
        raw_results = await self._vector_store.hybrid_search(
            query_text=query_text,
            filters=filters,
            limit=limit,
        )

        results = []
        for item in raw_results:
            results.append(
                RetrievalResult(
                    product_id=item["product_id"],
                    name=item["payload"].get("name", ""),
                    slug=item["payload"].get("slug", ""),
                    score=item["score"],
                    vector_score=item["score"],
                    metadata=item["payload"],
                    source="qdrant",
                )
            )

        logger.info("Hybrid search completed", candidates=len(results))
        return results
