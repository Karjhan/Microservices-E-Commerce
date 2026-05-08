"""
Recommendation Pipeline: product-to-product recommendations.

Seed → Qdrant Similar → Neo4j Expansion (category/material/printer) → Rerank → Return

Best flow: seed with a product, pull semantically similar from Qdrant,
expand through Neo4j for structural signals, then rerank with a blend of
semantic relevance, graph proximity, rating, and popularity.
"""

from app.core.settings import get_settings
from app.core.logging import get_logger
from app.domain.retrieval_result import RetrievalResult, SearchResponse
from app.vector.vector_store import VectorStore
from app.retrieval.graph_expansion import GraphExpansionStrategy
from app.retrieval.result_merger import merge_results
from app.ranking.reranker import Reranker
from app.ranking.scoring import apply_business_signals

logger = get_logger(__name__)


class RecommendationPipeline:
    """
    Product recommendation pipeline:
    1. Seed with a product ID
    2. Find semantically similar products (Qdrant)
    3. Expand through graph (shared category, material, printer)
    4. Rerank with cross-encoder + business signals
    """

    def __init__(
        self,
        vector_store: VectorStore,
        graph_expansion: GraphExpansionStrategy,
        reranker: Reranker,
    ) -> None:
        self._vector_store = vector_store
        self._graph_expansion = graph_expansion
        self._reranker = reranker
        self._settings = get_settings()

    async def execute(
        self,
        product_id: str,
        limit: int | None = None,
    ) -> SearchResponse:
        """Get recommendations for a product."""
        final_limit = limit or self._settings.final_top_k

        logger.info("Recommendation pipeline started", seed_product_id=product_id)

        # --- Stage 1: Vector similarity ---
        similar_raw = await self._vector_store.search_similar(
            product_id=product_id,
            limit=self._settings.hybrid_search_limit,
        )

        vector_candidates = [
            RetrievalResult(
                product_id=item["product_id"],
                name=item["payload"].get("name", ""),
                slug=item["payload"].get("slug", ""),
                score=item["score"],
                vector_score=item["score"],
                metadata=item["payload"],
                source="qdrant",
            )
            for item in similar_raw
        ]

        # --- Stage 2: Graph expansion ---
        graph_candidates = await self._graph_expansion.expand(
            candidates=vector_candidates,
            limit=self._settings.rerank_top_k,
        )

        # --- Merge ---
        merged = merge_results(vector_candidates, graph_candidates)
        total_candidates = len(merged)

        # --- Stage 3: Rerank ---
        # For recommendations, we use the seed product's name as the "query"
        seed_name = ""
        if vector_candidates:
            seed_name = vector_candidates[0].name or product_id

        reranked = self._reranker.rerank(
            query=f"products similar to {seed_name}",
            candidates=merged,
            top_k=self._settings.rerank_top_k,
        )

        # Apply business signals
        final_results = apply_business_signals(reranked)
        final_results = final_results[:final_limit]

        logger.info(
            "Recommendation pipeline completed",
            total_candidates=total_candidates,
            returned=len(final_results),
        )

        return SearchResponse(
            results=final_results,
            total_candidates=total_candidates,
            intent="recommendation",
        )
