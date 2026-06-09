import asyncio

import aiosmtplib
from aio_pika import connect_robust
from fastapi import APIRouter
from sqlalchemy import text

from app.api.schemas import HealthResponse
from app.core.settings import get_settings
from app.persistence.database import get_engine

router = APIRouter(tags=["Health"])


@router.get("/health", response_model=HealthResponse)
async def health_check() -> HealthResponse:
    settings = get_settings()
    postgres_status = await _check_postgres()
    rabbitmq_status = await _check_rabbitmq(settings.rabbitmq_url)
    smtp_status = await _check_smtp(settings.smtp_host, settings.smtp_port)

    statuses = [postgres_status, rabbitmq_status, smtp_status]
    if all(s == "healthy" for s in statuses):
        overall = "healthy"
    elif any(s.startswith("unhealthy") for s in statuses):
        overall = "degraded"
    else:
        overall = "degraded"

    return HealthResponse(
        status=overall,
        postgres=postgres_status,
        rabbitmq=rabbitmq_status,
        smtp=smtp_status,
    )


async def _check_postgres() -> str:
    try:
        engine = get_engine()
        async with engine.connect() as conn:
            await conn.execute(text("SELECT 1"))
        return "healthy"
    except Exception as exc:
        return f"unhealthy: {exc}"


async def _check_rabbitmq(url: str) -> str:
    try:
        connection = await asyncio.wait_for(connect_robust(url), timeout=5)
        await connection.close()
        return "healthy"
    except Exception as exc:
        return f"unhealthy: {exc}"


async def _check_smtp(host: str, port: int) -> str:
    try:
        smtp = aiosmtplib.SMTP(hostname=host, port=port, timeout=5)
        await asyncio.wait_for(smtp.connect(), timeout=5)
        await smtp.quit()
        return "healthy"
    except Exception as exc:
        return f"unhealthy: {exc}"
