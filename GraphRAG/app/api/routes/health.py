"""
Health check route: /health
"""

from fastapi import APIRouter
from app.api.schemas import HealthResponse
from app.core.settings import get_settings

router = APIRouter(tags=["health"])


@router.get("/health", response_model=HealthResponse)
async def health_check() -> HealthResponse:
    """Check connectivity to all backing services."""
    settings = get_settings()
    qdrant_status = "unknown"
    neo4j_status = "unknown"
    ollama_status = "unknown"

    # Check Qdrant
    try:
        from app.vector.qdrant_client import get_qdrant_client

        client = get_qdrant_client()
        client.get_collections()
        qdrant_status = "healthy"
    except Exception as e:
        qdrant_status = f"unhealthy: {e}"

    # Check Neo4j
    try:
        from app.graph.neo4j_client import get_neo4j_driver

        driver = await get_neo4j_driver()
        async with driver.session() as session:
            await session.run("RETURN 1")
        neo4j_status = "healthy"
    except Exception as e:
        neo4j_status = f"unhealthy: {e}"

    # Check Ollama
    try:
        import httpx

        async with httpx.AsyncClient() as client:
            resp = await client.get(f"{settings.ollama_base_url}/api/tags", timeout=5.0)
            if resp.status_code == 200:
                ollama_status = "healthy"
            else:
                ollama_status = f"unhealthy: status {resp.status_code}"
    except Exception as e:
        ollama_status = f"unhealthy: {e}"

    overall = "healthy" if all(
        s == "healthy" for s in [qdrant_status, neo4j_status, ollama_status]
    ) else "degraded"

    return HealthResponse(
        status=overall,
        qdrant=qdrant_status,
        neo4j=neo4j_status,
        ollama=ollama_status,
    )
