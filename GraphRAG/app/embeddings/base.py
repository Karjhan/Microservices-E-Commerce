from abc import ABC, abstractmethod


class EmbeddingProvider(ABC):
    """Abstract embedding provider interface."""

    @abstractmethod
    async def embed_text(self, text: str) -> list[float]:
        """Generate a dense embedding for a single text."""
        ...

    @abstractmethod
    async def embed_batch(self, texts: list[str]) -> list[list[float]]:
        """Generate dense embeddings for a batch of texts."""
        ...

    @abstractmethod
    async def embed_sparse(self, text: str) -> dict[int, float]:
        """Generate a sparse embedding (token_id → weight) for hybrid search."""
        ...
