import asyncio
from neo4j import AsyncGraphDatabase, AsyncDriver
from neo4j.exceptions import TransientError
from app.core.settings import get_settings
from app.core.logging import get_logger

logger = get_logger(__name__)

_driver: AsyncDriver | None = None


async def get_neo4j_driver() -> AsyncDriver:
    global _driver
    if _driver is None:
        settings = get_settings()
        _driver = AsyncGraphDatabase.driver(
            settings.neo4j_uri,
            auth=(settings.neo4j_user, settings.neo4j_password),
        )
        logger.info("Connected to Neo4j", uri=settings.neo4j_uri)
    return _driver


async def close_neo4j_driver() -> None:
    global _driver
    if _driver is not None:
        await _driver.close()
        _driver = None
        logger.info("Closed Neo4j connection")


async def ensure_constraints() -> None:
    driver = await get_neo4j_driver()
    statements = [
        (
            "CREATE CONSTRAINT product_id IF NOT EXISTS "
            "FOR (p:Product) REQUIRE p.id IS UNIQUE"
        ),
        (
            "CREATE CONSTRAINT category_id IF NOT EXISTS "
            "FOR (c:Category) REQUIRE c.id IS UNIQUE"
        ),
        (
            "CREATE CONSTRAINT attribute_key_value IF NOT EXISTS "
            "FOR (a:Attribute) REQUIRE (a.key, a.value) IS UNIQUE"
        ),
        "CREATE INDEX material_name IF NOT EXISTS FOR (m:Material) ON (m.name)",
        "CREATE INDEX printer_name IF NOT EXISTS FOR (pr:Printer) ON (pr.name)",
    ]
    max_attempts = 5
    for attempt in range(1, max_attempts + 1):
        try:
            async with driver.session() as session:
                for stmt in statements:
                    await session.run(stmt)
            logger.info("Neo4j constraints and indexes ensured")
            return
        except TransientError as exc:
            if attempt == max_attempts:
                raise
            wait = attempt * 0.5
            logger.warning(
                "Neo4j transient error during ensure_constraints, retrying",
                attempt=attempt,
                wait_seconds=wait,
                error=str(exc),
            )
            await asyncio.sleep(wait)