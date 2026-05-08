from fastapi import APIRouter, Depends
from app.api.schemas import ChatRequest, ChatResponse, ProductResult
from app.api.dependencies import get_chat_pipeline
from app.pipeline.chat_pipeline import ChatPipeline

router = APIRouter(prefix="/api", tags=["chat"])


@router.post("/chat", response_model=ChatResponse)
async def chat(
    request: ChatRequest,
    pipeline: ChatPipeline = Depends(get_chat_pipeline),
) -> ChatResponse:
    result = await pipeline.execute(
        query=request.query,
        filters=request.filters,
    )

    return ChatResponse(
        answer=result.answer or "I couldn't generate an answer.",
        sources=[
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
        intent=result.intent,
    )
