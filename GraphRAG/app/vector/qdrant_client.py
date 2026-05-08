from qdrant_client import QdrantClient, models
from app.core.settings import get_settings
from app.core.logging import get_logger

logger = get_logger(__name__)

_client: QdrantClient | None = None

DENSE_VECTOR_SIZE = 1024
COLLECTION_NAME: str = ""


def get_qdrant_client() -> QdrantClient:
    global _client, COLLECTION_NAME
    if _client is None:
        settings = get_settings()
        COLLECTION_NAME = settings.qdrant_collection
        _client = QdrantClient(host=settings.qdrant_host, port=settings.qdrant_port)
        logger.info(
            "Connected to Qdrant",
            host=settings.qdrant_host,
            port=settings.qdrant_port,
        )
    return _client


async def ensure_collection() -> None:
    """Create the products collection if it doesn't exist, with named vectors."""
    client = get_qdrant_client()
    settings = get_settings()
    collection_name = settings.qdrant_collection

    collections = client.get_collections().collections
    existing_names = [c.name for c in collections]

    if collection_name not in existing_names:
        client.create_collection(
            collection_name=collection_name,
            vectors_config={
                "dense": models.VectorParams(
                    size=DENSE_VECTOR_SIZE,
                    distance=models.Distance.COSINE,
                ),
            },
            sparse_vectors_config={
                "sparse": models.SparseVectorParams(
                    modifier=models.Modifier.IDF,
                ),
            },
        )

        for field_name in [
            "category_id",
            "status",
            "tags",
            "supported_materials",
            "compatible_printers",
            "currency",
        ]:
            client.create_payload_index(
                collection_name=collection_name,
                field_name=field_name,
                field_schema=models.PayloadSchemaType.KEYWORD,
            )

        for field_name in ["price", "average_rating", "download_count"]:
            client.create_payload_index(
                collection_name=collection_name,
                field_name=field_name,
                field_schema=models.PayloadSchemaType.FLOAT,
            )

        logger.info("Created Qdrant collection", collection=collection_name)
    else:
        logger.info("Qdrant collection already exists", collection=collection_name)
