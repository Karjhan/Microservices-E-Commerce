from pathlib import Path
from unittest.mock import AsyncMock, MagicMock
from uuid import uuid4

import pytest
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine

from app.application.idempotency_service import IdempotencyService
from app.application.template_renderer import TemplateRenderer
from app.domain.enums import ProcessedOutcome
from app.persistence.models import Base


@pytest.fixture
def templates_dir() -> Path:
    return Path(__file__).resolve().parents[1] / "app" / "templates"


@pytest.fixture
async def db_session():
    engine = create_async_engine("sqlite+aiosqlite:///:memory:")
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)
    factory = async_sessionmaker(engine, expire_on_commit=False)
    async with factory() as session:
        yield session
    await engine.dispose()
