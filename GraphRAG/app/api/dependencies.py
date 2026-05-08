from functools import lru_cache
from app.embeddings.service import get_embedding_provider
from app.vector.vector_store import VectorStore
from app.graph.graph_repository import GraphRepository
from app.generation.llm_client import LLMClient
from app.generation.intent_classifier import IntentClassifier
from app.generation.query_rewriter import QueryRewriter
from app.generation.answer_generator import AnswerGenerator
from app.retrieval.hybrid_search import HybridSearchStrategy
from app.retrieval.graph_expansion import GraphExpansionStrategy
from app.pipeline.search_pipeline import SearchPipeline
from app.pipeline.recommendation_pipeline import RecommendationPipeline
from app.pipeline.chat_pipeline import ChatPipeline


@lru_cache
def get_llm_client() -> LLMClient:
    return LLMClient()


@lru_cache
def get_vector_store() -> VectorStore:
    embedding_provider = get_embedding_provider()
    return VectorStore(embedding_provider)


@lru_cache
def get_graph_repository() -> GraphRepository:
    return GraphRepository()


@lru_cache
def get_reranker():
    from app.ranking.reranker import Reranker
    return Reranker()


def get_search_pipeline() -> SearchPipeline:
    llm = get_llm_client()
    return SearchPipeline(
        intent_classifier=IntentClassifier(llm),
        query_rewriter=QueryRewriter(llm),
        hybrid_search=HybridSearchStrategy(get_vector_store()),
        graph_expansion=GraphExpansionStrategy(get_graph_repository()),
        reranker=get_reranker(),
    )


def get_recommendation_pipeline() -> RecommendationPipeline:
    return RecommendationPipeline(
        vector_store=get_vector_store(),
        graph_expansion=GraphExpansionStrategy(get_graph_repository()),
        reranker=get_reranker(),
    )


def get_chat_pipeline() -> ChatPipeline:
    return ChatPipeline(
        search_pipeline=get_search_pipeline(),
        answer_generator=AnswerGenerator(get_llm_client()),
    )
