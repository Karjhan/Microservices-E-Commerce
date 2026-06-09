from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api.routes import health, notifications
from app.core.logging import configure_logging, get_logger
from app.persistence.database import dispose_engine

logger = get_logger(__name__)

app = FastAPI(
    title="Notifications Service",
    description="Email notifications microservice — health and delivery history",
    version="1.0.0",
)


@asynccontextmanager
async def lifespan(fastapi_app: FastAPI):
    configure_logging()
    from app.core.telemetry import configure_telemetry

    configure_telemetry(fastapi_app)
    logger.info("Notifications API starting up")
    yield
    await dispose_engine()
    logger.info("Notifications API shut down")


app.router.lifespan_context = lifespan

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(health.router)
app.include_router(notifications.router)
