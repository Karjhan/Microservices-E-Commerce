"""
python -m scripts.reindex
"""

import asyncio
from app.core.logging import configure_logging, get_logger
from app.core.settings import get_settings
from app.embeddings.service import get_embedding_provider
from app.vector.vector_store import VectorStore
from app.vector.qdrant_client import ensure_collection, get_qdrant_client
from app.graph.neo4j_client import get_neo4j_driver
from app.domain.product_document import ProductDocument, ProductAttribute
from app.ingestion.attribute_classifier import classify_attribute

logger = get_logger(__name__)


async def reindex() -> None:
    configure_logging()
    logger.info("Starting full reindex from Neo4j → Qdrant")

    settings = get_settings()
    await ensure_collection()

    driver = await get_neo4j_driver()
    embedding_provider = get_embedding_provider()
    vector_store = VectorStore(embedding_provider)

    async with driver.session() as session:
        result = await session.run("""
            MATCH (p:Product)
            OPTIONAL MATCH (p)-[:BELONGS_TO]->(c:Category)
            OPTIONAL MATCH (p)-[:SUPPORTS_MATERIAL]->(m:Material)
            OPTIONAL MATCH (p)-[:COMPATIBLE_WITH]->(pr:Printer)
            OPTIONAL MATCH (p)-[:HAS_ATTRIBUTE]->(a:Attribute)
            RETURN p,
                   c.id AS category_id,
                   collect(DISTINCT m.name) AS materials,
                   collect(DISTINCT pr.name) AS printers,
                   collect(DISTINCT {key: a.key, value: a.value}) AS attributes
        """)

        count = 0
        async for record in result:
            product_props = dict(record["p"])

            attributes = [
                ProductAttribute(
                    key=a["key"],
                    value=a["value"],
                    bucket=classify_attribute(a["key"]),
                )
                for a in record["attributes"]
                if a["key"] is not None
            ]

            document = ProductDocument(
                id=product_props["id"],
                name=product_props.get("name", ""),
                slug=product_props.get("slug", ""),
                short_description=product_props.get("short_description", ""),
                long_description="",  # Not stored in Neo4j by default
                price=product_props.get("price", 0),
                currency=product_props.get("currency", "USD"),
                category_id=record["category_id"] or "",
                status=product_props.get("status", "active"),
                tags=product_props.get("tags", []),
                supported_materials=record["materials"],
                compatible_printers=record["printers"],
                attributes=attributes,
                download_count=product_props.get("download_count", 0),
                average_rating=product_props.get("average_rating", 0),
            )

            await vector_store.upsert_product(document)
            count += 1
            if count % 100 == 0:
                logger.info(f"Reindexed {count} products")

    logger.info(f"Reindex complete. Total products: {count}")


if __name__ == "__main__":
    asyncio.run(reindex())
