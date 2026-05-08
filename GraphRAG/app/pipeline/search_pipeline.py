from app.core.settings import get_settings
from app.core.logging import get_logger
from app.domain.search_query import SearchQuery, SearchIntent
from app.domain.retrieval_result import SearchResponse
from app.generation.intent_classifier import IntentClassifier
from app.generation.query_rewriter import QueryRewriter
from app.retrieval.hybrid_search import HybridSearchStrategy
from app.retrieval.graph_expansion import GraphExpansionStrategy
from app.retrieval.result_merger import merge_results
from app.retrieval.metadata_filters import build_qdrant_filters
from app.ranking.reranker import Reranker
from app.ranking.scoring import apply_business_signals

logger = get_logger(__name__)


class SearchPipeline:
    """
    Full 4-stage search pipeline:
    1. Query understanding (intent + rewrite)
    2. Candidate generation (Qdrant hybrid search)
    3. Graph expansion (Neo4j traversal)
    4. Reranking (cross-encoder + business signals)
    """

    def __init__(
        self,
        intent_classifier: IntentClassifier,
        query_rewriter: QueryRewriter,
        hybrid_search: HybridSearchStrategy,
        graph_expansion: GraphExpansionStrategy,
        reranker: Reranker,
    ) -> None:
        self._intent_classifier = intent_classifier
        self._query_rewriter = query_rewriter
        self._hybrid_search = hybrid_search
        self._graph_expansion = graph_expansion
        self._reranker = reranker
        self._settings = get_settings()

    async def execute(
        self,
        raw_query: str,
        filters: dict | None = None,
        limit: int | None = None,
    ) -> SearchResponse:
        final_limit = limit or self._settings.final_top_k

        # --- Stage 1: Query Understanding ---
        intent = await self._intent_classifier.classify(raw_query)
        rewritten_query = await self._query_rewriter.rewrite(raw_query)

        search_query = SearchQuery(
            raw_query=raw_query,
            rewritten_query=rewritten_query,
            intent=intent,
            filters=filters or {},
            limit=final_limit,
        )

        logger.info(
            "Search pipeline started",
            intent=intent.value,
            rewritten=rewritten_query,
        )

        # --- Stage 2: Candidate Generation (Qdrant) ---
        qdrant_filters = build_qdrant_filters(search_query.filters)
        vector_candidates = await self._hybrid_search.search(
            query_text=search_query.effective_query,
            filters=qdrant_filters if qdrant_filters else None,
            limit=self._settings.hybrid_search_limit,
        )

        # --- Stage 3: Graph Expansion (Neo4j) ---
        graph_candidates = []
        if intent in (
            SearchIntent.RELATION_LOOKUP,
            SearchIntent.RECOMMENDATION,
            SearchIntent.SEMANTIC_BROWSE,
        ):
            graph_candidates = await self._graph_expansion.expand(
                candidates=vector_candidates,
                limit=self._settings.rerank_top_k,
            )

        # --- Merge ---
        merged = merge_results(vector_candidates, graph_candidates)
        total_candidates = len(merged)

        # --- Stage 4: Reranking ---
        reranked = self._reranker.rerank(
            query=search_query.effective_query,
            candidates=merged,
            top_k=self._settings.rerank_top_k,
        )

        final_results = apply_business_signals(reranked)
        final_results = final_results[:final_limit]

        logger.info(
            "Search pipeline completed",
            total_candidates=total_candidates,
            returned=len(final_results),
        )

        return SearchResponse(
            results=final_results,
            total_candidates=total_candidates,
            intent=intent.value,
        )
