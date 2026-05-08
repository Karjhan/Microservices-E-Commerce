from qdrant_client import models
from app.core.settings import get_settings
from app.core.logging import get_logger
from app.domain.product_document import ProductDocument
from app.embeddings.base import EmbeddingProvider
from app.vector.qdrant_client import get_qdrant_client

logger = get_logger(__name__)


class VectorStore:
    def __init__(self, embedding_provider: EmbeddingProvider) -> None:
        self._embeddings = embedding_provider
        self._client = get_qdrant_client()
        self._collection = get_settings().qdrant_collection

    async def upsert_product(self, document: ProductDocument) -> None:
        embedding_text = document.to_embedding_text()
        dense_vector = await self._embeddings.embed_text(embedding_text)
        sparse_vector = await self._embeddings.embed_sparse(embedding_text)

        vectors: dict = {"dense": dense_vector}
        sparse_vectors = {}
        if sparse_vector:
            sparse_vectors["sparse"] = models.SparseVector(
                indices=list(sparse_vector.keys()),
                values=list(sparse_vector.values()),
            )

        point = models.PointStruct(
            id=document.id,
            vector=vectors,
            payload=document.to_qdrant_payload(),
        )

        if sparse_vectors:
            point.vector.update(sparse_vectors)

        self._client.upsert(
            collection_name=self._collection,
            points=[point],
        )
        logger.info("Upserted product to Qdrant", product_id=document.id)

    async def delete_product(self, product_id: str) -> None:
        self._client.delete(
            collection_name=self._collection,
            points_selector=models.PointIdsList(points=[product_id]),
        )
        logger.info("Deleted product from Qdrant", product_id=product_id)

    async def hybrid_search(
        self,
        query_text: str,
        filters: dict | None = None,
        limit: int = 50,
    ) -> list[dict]:
        dense_vector = await self._embeddings.embed_text(query_text)
        sparse_vector = await self._embeddings.embed_sparse(query_text)

        must_conditions = []
        if filters:
            for key, value in filters.items():
                if isinstance(value, list):
                    must_conditions.append(
                        models.FieldCondition(
                            key=key,
                            match=models.MatchAny(any=value),
                        )
                    )
                elif isinstance(value, dict) and ("gte" in value or "lte" in value):
                    must_conditions.append(
                        models.FieldCondition(
                            key=key,
                            range=models.Range(
                                gte=value.get("gte"),
                                lte=value.get("lte"),
                            ),
                        )
                    )
                else:
                    must_conditions.append(
                        models.FieldCondition(
                            key=key,
                            match=models.MatchValue(value=value),
                        )
                    )

        query_filter = models.Filter(must=must_conditions) if must_conditions else None

        if sparse_vector:
            results = self._client.query_points(
                collection_name=self._collection,
                prefetch=[
                    models.Prefetch(
                        query=dense_vector,
                        using="dense",
                        limit=limit,
                        filter=query_filter,
                    ),
                    models.Prefetch(
                        query=models.SparseVector(
                            indices=list(sparse_vector.keys()),
                            values=list(sparse_vector.values()),
                        ),
                        using="sparse",
                        limit=limit,
                        filter=query_filter,
                    ),
                ],
                query=models.FusionQuery(fusion=models.Fusion.RRF),
                limit=limit,
            )
        else:
            results = self._client.query_points(
                collection_name=self._collection,
                query=dense_vector,
                using="dense",
                query_filter=query_filter,
                limit=limit,
            )

        return [
            {
                "product_id": point.id,
                "score": point.score,
                "payload": point.payload,
            }
            for point in results.points
        ]

    async def search_similar(
        self, product_id: str, limit: int = 20
    ) -> list[dict]:
        records = self._client.retrieve(
            collection_name=self._collection,
            ids=[product_id],
            with_vectors=["dense"],
        )
        if not records:
            logger.warning("Product not found in Qdrant for similarity search", product_id=product_id)
            return []

        dense_vector = records[0].vector["dense"]  

        results = self._client.query_points(
            collection_name=self._collection,
            query=dense_vector,
            using="dense",
            query_filter=models.Filter(
                must_not=[models.HasIdCondition(has_id=[product_id])]
            ),
            limit=limit,
        )
        return [
            {
                "product_id": point.id,
                "score": point.score,
                "payload": point.payload,
            }
            for point in results.points
        ]