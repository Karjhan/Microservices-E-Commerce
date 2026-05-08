from app.embeddings.base import EmbeddingProvider
from app.embeddings.ollama_provider import OllamaEmbeddingProvider
from app.core.logging import get_logger

logger = get_logger(__name__)

_provider: EmbeddingProvider | None = None


def get_embedding_provider(use_native_bge: bool = False) -> EmbeddingProvider:
    global _provider
    if _provider is None:
        if use_native_bge:
            from app.embeddings.bge_m3_provider import BGEM3EmbeddingProvider
            _provider = BGEM3EmbeddingProvider()
        else:
            _provider = OllamaEmbeddingProvider()
        logger.info("Initialized embedding provider", provider=type(_provider).__name__)
    return _provider
