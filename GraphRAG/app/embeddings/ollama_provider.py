import ollama
from app.core.settings import get_settings
from app.core.logging import get_logger
from app.embeddings.base import EmbeddingProvider

logger = get_logger(__name__)


class OllamaEmbeddingProvider(EmbeddingProvider):
    """
    Embedding provider using Ollama with BGE-M3.
    BGE-M3 supports dense, multi-vector, and sparse retrieval in one model.
    """

    def __init__(self) -> None:
        settings = get_settings()
        self._client = ollama.AsyncClient(host=settings.ollama_base_url)
        self._model = settings.embedding_model

    async def embed_text(self, text: str) -> list[float]:
        response = await self._client.embed(model=self._model, input=text)
        return response["embeddings"][0]

    async def embed_batch(self, texts: list[str]) -> list[list[float]]:
        response = await self._client.embed(model=self._model, input=texts)
        return response["embeddings"]

    async def embed_sparse(self, text: str) -> dict[int, float]:
        """
        Generate sparse representation for hybrid search.
        For BGE-M3 through Ollama, we use a simple term-frequency approach
        as a fallback. When using FlagEmbedding directly, the model provides
        native sparse vectors.
        """
        # Ollama's embed endpoint does not natively return sparse vectors.
        # For production, use FlagEmbedding directly for sparse output.
        # This is a placeholder that returns an empty sparse vector,
        # letting Qdrant's hybrid search fall back to dense-only.
        return {}
