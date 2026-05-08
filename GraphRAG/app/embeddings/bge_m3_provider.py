from FlagEmbedding import BGEM3FlagModel
from app.core.logging import get_logger
from app.embeddings.base import EmbeddingProvider

logger = get_logger(__name__)


class BGEM3EmbeddingProvider(EmbeddingProvider):
    """
    Direct BGE-M3 embedding provider using FlagEmbedding library.
    Provides native dense + sparse vectors for true hybrid search.
    Preferred over Ollama when you need sparse retrieval.
    """

    def __init__(self, model_name: str = "BAAI/bge-m3", use_fp16: bool = True) -> None:
        logger.info("Loading BGE-M3 model", model=model_name)
        self._model = BGEM3FlagModel(model_name, use_fp16=use_fp16)

    async def embed_text(self, text: str) -> list[float]:
        result = self._model.encode(
            [text],
            return_dense=True,
            return_sparse=False,
            return_colbert_vecs=False,
        )
        return result["dense_vecs"][0].tolist()

    async def embed_batch(self, texts: list[str]) -> list[list[float]]:
        result = self._model.encode(
            texts,
            return_dense=True,
            return_sparse=False,
            return_colbert_vecs=False,
        )
        return [vec.tolist() for vec in result["dense_vecs"]]

    async def embed_sparse(self, text: str) -> dict[int, float]:
        """Generate native sparse embedding from BGE-M3."""
        result = self._model.encode(
            [text],
            return_dense=False,
            return_sparse=True,
            return_colbert_vecs=False,
        )
        # FlagEmbedding returns sparse as a list of dicts {token_id: weight}
        sparse_dict = result["lexical_weights"][0]
        return {int(k): float(v) for k, v in sparse_dict.items()}
