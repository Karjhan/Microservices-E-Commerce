import uuid

import pytest
from httpx import ASGITransport, AsyncClient
from sqlalchemy.ext.asyncio import async_sessionmaker, create_async_engine

from app.api.dependencies import get_db_session
from app.api.main import app
from app.domain.enums import DeliveryStatus, NotificationType
from app.persistence.models import Base, Notification


@pytest.fixture
async def api_client():
    engine = create_async_engine("sqlite+aiosqlite:///:memory:")
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)
    factory = async_sessionmaker(engine, expire_on_commit=False)

    async def override_session():
        async with factory() as session:
            yield session

    app.dependency_overrides[get_db_session] = override_session

    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        yield client, factory

    app.dependency_overrides.clear()
    await engine.dispose()


@pytest.mark.asyncio
async def test_history_api_filters(api_client):
    client, factory = api_client
    order_id = uuid.uuid4()

    async with factory() as session:
        session.add(
            Notification(
                id=uuid.uuid4(),
                idempotency_key=f"order.created:{order_id}",
                event_type="order.created",
                notification_type=NotificationType.ORDER_CONFIRMATION.value,
                channel="Email",
                recipient="a@example.com",
                subject="Order confirmed",
                status=DeliveryStatus.SENT.value,
                attempt_count=1,
            )
        )
        session.add(
            Notification(
                id=uuid.uuid4(),
                idempotency_key=f"user.registered:{uuid.uuid4()}",
                event_type="user.registered",
                notification_type=NotificationType.WELCOME.value,
                channel="Email",
                recipient="b@example.com",
                subject="Welcome",
                status=DeliveryStatus.SENT.value,
                attempt_count=1,
            )
        )
        await session.commit()

    response = await client.get("/notifications", params={"eventType": "order.created"})
    assert response.status_code == 200
    body = response.json()
    assert body["totalCount"] == 1
    assert body["items"][0]["eventType"] == "order.created"
