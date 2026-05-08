from app.core.logging import get_logger
from app.domain.product_document import ProductDocument
from app.graph.neo4j_client import get_neo4j_driver
from app.graph import cypher_queries as cq

logger = get_logger(__name__)


class GraphRepository:
    async def upsert_product(self, document: ProductDocument) -> None:
        """Create or update a product node with all its relationships."""
        driver = await get_neo4j_driver()
        async with driver.session() as session:
            await session.run(
                cq.UPSERT_PRODUCT,
                id=document.id,
                name=document.name,
                slug=document.slug,
                short_description=document.short_description,
                price=document.price,
                currency=document.currency,
                status=document.status,
                category_id=document.category_id,
                download_count=document.download_count,
                average_rating=document.average_rating,
                tags=document.tags,
            )

            if document.supported_materials:
                await session.run(
                    cq.SYNC_MATERIALS,
                    product_id=document.id,
                    materials=document.supported_materials,
                )

            if document.compatible_printers:
                await session.run(
                    cq.SYNC_PRINTERS,
                    product_id=document.id,
                    printers=document.compatible_printers,
                )

            for attr in document.attributes:
                await session.run(
                    cq.ADD_ATTRIBUTE,
                    product_id=document.id,
                    key=attr.key,
                    value=attr.value,
                )

        logger.info("Upserted product to Neo4j", product_id=document.id)

    async def delete_product(self, product_id: str) -> None:
        driver = await get_neo4j_driver()
        async with driver.session() as session:
            await session.run(cq.DELETE_PRODUCT, id=product_id)
        logger.info("Deleted product from Neo4j", product_id=product_id)

    async def add_relation(
        self,
        product_id: str,
        related_product_id: str,
        relation_type: str,
        relation_id: str,
    ) -> None:
        driver = await get_neo4j_driver()
        async with driver.session() as session:
            await session.run(
                cq.ADD_RELATION,
                product_id=product_id,
                related_product_id=related_product_id,
                relation_type=relation_type,
                relation_id=relation_id,
            )
        logger.info(
            "Added relation",
            product_id=product_id,
            related_product_id=related_product_id,
            type=relation_type,
        )

    async def delete_relation(
        self,
        product_id: str,
        related_product_id: str,
        relation_id: str,
    ) -> None:
        driver = await get_neo4j_driver()
        async with driver.session() as session:
            await session.run(
                cq.DELETE_RELATION,
                product_id=product_id,
                related_product_id=related_product_id,
                relation_id=relation_id,
            )
        logger.info(
            "Deleted relation",
            product_id=product_id,
            related_product_id=related_product_id,
        )

    async def add_attribute(
        self, product_id: str, key: str, value: str
    ) -> None:
        """Add an attribute node and connect it to the product."""
        driver = await get_neo4j_driver()
        async with driver.session() as session:
            await session.run(
                cq.ADD_ATTRIBUTE,
                product_id=product_id,
                key=key,
                value=value,
            )
        logger.info("Added attribute", product_id=product_id, key=key)

    async def delete_attribute(self, product_id: str, key: str) -> None:
        """Delete an attribute relationship from the product."""
        driver = await get_neo4j_driver()
        async with driver.session() as session:
            await session.run(
                cq.DELETE_ATTRIBUTE,
                product_id=product_id,
                key=key,
            )
        logger.info("Deleted attribute", product_id=product_id, key=key)

    async def score_candidates(self, product_ids: list[str]) -> dict[str, float]:
        """
        For each candidate, count how many of the OTHER candidates it is directly
        connected to (RELATED_TO, COMPATIBLE_WITH, SUPPORTS_MATERIAL, BELONGS_TO).
        Returns a normalized [0, 1] graph_score keyed by product_id.
        Products with more intra-result connections score higher.
        """
        if not product_ids:
            return {}
        driver = await get_neo4j_driver()
        async with driver.session() as session:
            result = await session.run(
                cq.SCORE_CANDIDATES,
                product_ids=product_ids,
            )
            rows = await result.data()

        if not rows:
            return {}

        counts: dict[str, int] = {row["product_id"]: row["connection_count"] for row in rows}
        max_count = max(counts.values()) if counts else 1
        if max_count == 0:
            return {pid: 0.0 for pid in counts}
        return {pid: round(count / max_count, 4) for pid, count in counts.items()}

    async def expand_products(
        self, product_ids: list[str], limit: int = 20
    ) -> list[dict]:
        """
        Graph expansion: find neighbors of the given products via
        relations, shared category, shared material, shared printer.
        Returns candidate products not in the original set.
        """
        driver = await get_neo4j_driver()
        async with driver.session() as session:
            result = await session.run(
                cq.EXPAND_MULTIPLE_PRODUCTS,
                product_ids=product_ids,
                limit=limit,
            )
            records = await result.data()
            return [
                {
                    "product_id": r["product_id"],
                    "name": r["name"],
                    "slug": r["slug"],
                    "average_rating": r.get("average_rating", 0),
                    "download_count": r.get("download_count", 0),
                }
                for r in records
            ]

    async def get_product_context(self, product_id: str) -> dict | None:
        driver = await get_neo4j_driver()
        async with driver.session() as session:
            result = await session.run(
                cq.GET_PRODUCT_CONTEXT, product_id=product_id
            )
            record = await result.single()
            if record is None:
                return None
            return {
                "product": dict(record["p"]),
                "category_id": record["category_id"],
                "materials": record["materials"],
                "printers": record["printers"],
                "attributes": record["attributes"],
                "relations": record["relations"],
            }