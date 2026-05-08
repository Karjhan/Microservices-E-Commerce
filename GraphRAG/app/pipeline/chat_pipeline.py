from app.core.logging import get_logger
from app.domain.retrieval_result import SearchResponse
from app.pipeline.search_pipeline import SearchPipeline
from app.generation.answer_generator import AnswerGenerator

logger = get_logger(__name__)


class ChatPipeline:
    """
    Conversational RAG pipeline:
    1. Run search pipeline for retrieval
    2. Build context from top results
    3. Generate answer with LLM (grounded in context)
    """

    def __init__(
        self,
        search_pipeline: SearchPipeline,
        answer_generator: AnswerGenerator,
    ) -> None:
        self._search_pipeline = search_pipeline
        self._answer_generator = answer_generator

    async def execute(
        self,
        query: str,
        filters: dict | None = None,
    ) -> SearchResponse:
        logger.info("Chat pipeline started", query=query)

        search_response = await self._search_pipeline.execute(
            raw_query=query,
            filters=filters,
            limit=10,
        )

        answer = await self._answer_generator.generate_answer(
            query=query,
            results=search_response.results,
        )

        search_response.answer = answer

        logger.info("Chat pipeline completed", answer_length=len(answer))
        return search_response
