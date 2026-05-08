from contextlib import asynccontextmanager
from fastapi import FastAPI
from app.core.logging import configure_logging, get_logger
from app.vector.qdrant_client import ensure_collection
from app.graph.neo4j_client import ensure_constraints, close_neo4j_driver
from app.api.routes import search, recommend, chat, health

logger = get_logger(__name__)

# Build app first so FastAPIInstrumentor can reference it during configure_telemetry
app = FastAPI(
    title="GraphRAG Service",
    description="Dual-index GraphRAG: Qdrant (semantic) + Neo4j (graph) for 3D printing catalog",
    version="0.1.0",
)

@asynccontextmanager
async def lifespan(fastapi_app: FastAPI):
    """Application startup and shutdown."""
    configure_logging()
    from app.core.telemetry import configure_telemetry
    configure_telemetry(fastapi_app)

    logger.info("GraphRAG service starting up")

    await ensure_collection()
    await ensure_constraints()

    logger.info("GraphRAG service ready")
    yield

    await close_neo4j_driver()
    logger.info("GraphRAG service shut down")


app.router.lifespan_context = lifespan

app.include_router(health.router)
app.include_router(search.router)
app.include_router(recommend.router)
app.include_router(chat.router)
