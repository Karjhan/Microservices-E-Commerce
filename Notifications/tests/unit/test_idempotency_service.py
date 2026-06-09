from uuid import uuid4

import pytest

from app.application.idempotency_service import IdempotencyService
from app.domain.enums import ProcessedOutcome


@pytest.mark.asyncio
async def test_idempotency_detects_duplicate(db_session):
    service = IdempotencyService()
    key = service.build_key("user.registered", str(uuid4()))

    assert await service.is_processed(db_session, key) is False
    assert await service.mark_processed(
        db_session,
        idempotency_key=key,
        event_type="user.registered",
        outcome=ProcessedOutcome.SENT,
    )
    await db_session.commit()

    assert await service.is_processed(db_session, key) is True


@pytest.mark.asyncio
async def test_concurrent_insert_returns_false_on_duplicate(db_session):
    service = IdempotencyService()
    key = service.build_key("order.created", str(uuid4()))

    first = await service.mark_processed(
        db_session,
        idempotency_key=key,
        event_type="order.created",
        outcome=ProcessedOutcome.SENT,
    )
    await db_session.commit()
    assert first is True

    second = await service.mark_processed(
        db_session,
        idempotency_key=key,
        event_type="order.created",
        outcome=ProcessedOutcome.SENT,
    )
    assert second is False
