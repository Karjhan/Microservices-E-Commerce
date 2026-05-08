"""
Search route: /api/search
"""

from fastapi import APIRouter, Depends
from app.api.schemas import SearchRequest, SearchResponse, ProductResult
from app.api.dependencies import get_search_pipeline
from app.pipeline.search_pipeline import SearchPipeline

router = APIRouter(prefix="/api", tags=["search"])


@router.post("/search", response_model=SearchResponse)
async def search(
    request: SearchRequest,
    pipeline: SearchPipeline = Depends(get_search_pipeline),
) -> SearchResponse:
    """
    Full hybrid search: semantic + graph + reranking.
    Supports payload filtering by materials, printers, category, price, etc.
    """
    result = await pipeline.execute(
        raw_query=request.query,
        filters=request.filters,
        limit=request.limit,
    )

    return SearchResponse(
        results=[
            ProductResult(
                product_id=r.product_id,
                name=r.name,
                slug=r.slug,
                score=r.score,
                vector_score=r.vector_score,
                graph_score=r.graph_score,
                rerank_score=r.rerank_score,
                metadata=r.metadata,
            )
            for r in result.results
        ],
        total_candidates=result.total_candidates,
        intent=result.intent,
    )
