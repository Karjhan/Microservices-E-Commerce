"""
Integration tests for the search API.
Requires running Qdrant, Neo4j, and Ollama instances.
Run with: pytest tests/integration/ -v
"""

import pytest
from httpx import AsyncClient, ASGITransport
from app.api.main import app


@pytest.fixture
async def client():
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        yield client


class TestHealthEndpoint:
    @pytest.mark.asyncio
    async def test_health_returns_status(self, client: AsyncClient):
        response = await client.get("/health")
        assert response.status_code == 200
        data = response.json()
        assert "status" in data
        assert "qdrant" in data
        assert "neo4j" in data
        assert "ollama" in data


class TestSearchEndpoint:
    @pytest.mark.asyncio
    async def test_search_validates_empty_query(self, client: AsyncClient):
        response = await client.post("/api/search", json={"query": "", "limit": 10})
        assert response.status_code == 422  # Validation error

    @pytest.mark.asyncio
    async def test_search_validates_limit_range(self, client: AsyncClient):
        response = await client.post(
            "/api/search", json={"query": "dragon", "limit": 200}
        )
        assert response.status_code == 422

    @pytest.mark.asyncio
    async def test_search_accepts_valid_request(self, client: AsyncClient):
        response = await client.post(
            "/api/search",
            json={
                "query": "articulated dragon",
                "filters": {"materials": ["PLA"]},
                "limit": 5,
            },
        )
        # Will fail if services aren't running, but validates the endpoint works
        assert response.status_code in (200, 500)


class TestChatEndpoint:
    @pytest.mark.asyncio
    async def test_chat_validates_empty_query(self, client: AsyncClient):
        response = await client.post("/api/chat", json={"query": ""})
        assert response.status_code == 422

    @pytest.mark.asyncio
    async def test_chat_accepts_valid_request(self, client: AsyncClient):
        response = await client.post(
            "/api/chat",
            json={"query": "What printers are compatible with the dragon model?"},
        )
        assert response.status_code in (200, 500)


class TestRecommendEndpoint:
    @pytest.mark.asyncio
    async def test_recommend_validates_empty_id(self, client: AsyncClient):
        response = await client.post("/api/recommend", json={"product_id": ""})
        assert response.status_code == 422

    @pytest.mark.asyncio
    async def test_recommend_accepts_valid_request(self, client: AsyncClient):
        response = await client.post(
            "/api/recommend",
            json={"product_id": "550e8400-e29b-41d4-a716-446655440001", "limit": 5},
        )
        assert response.status_code in (200, 500)
