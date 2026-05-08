"""
Recommendation route: /api/recommend
"""

from fastapi import APIRouter, Depends
from app.api.schemas import RecommendRequest, SearchResponse, ProductResult
from app.api.dependencies import get_recommendation_pipeline
from app.pipeline.recommendation_pipeline import RecommendationPipeline

router = APIRouter(prefix="/api", tags=["recommendations"])


@router.post("/recommend", response_model=SearchResponse)
async def recommend(
    request: RecommendRequest,
    pipeline: RecommendationPipeline = Depends(get_recommendation_pipeline),
) -> SearchResponse:
    """
    Get product recommendations based on a seed product.
    Uses vector similarity + graph expansion + reranking.
    """
    result = await pipeline.execute(
        product_id=request.product_id,
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
