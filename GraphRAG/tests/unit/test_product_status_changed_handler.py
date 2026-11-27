"""Unit tests for the product status sync handler (FR-019)."""

from unittest.mock import AsyncMock

from app.ingestion.handlers.product_status_changed_handler import ProductStatusChangedHandler


def _handler():
    vector_store = AsyncMock()
    graph_repo = AsyncMock()
    return ProductStatusChangedHandler(vector_store, graph_repo), vector_store, graph_repo


class TestProductStatusChangedHandler:
    async def test_updates_status_in_both_stores(self):
        handler, vector_store, graph_repo = _handler()

        await handler.handle({"ProductId": "p-1", "Status": "Active", "IsVisible": True})

        vector_store.set_status.assert_awaited_once_with("p-1", "Active")
        graph_repo.set_status.assert_awaited_once_with("p-1", "Active")

    async def test_unpublish_marks_product_inactive(self):
        handler, vector_store, graph_repo = _handler()

        await handler.handle({"ProductId": "p-2", "Status": "Inactive", "IsVisible": False})

        vector_store.set_status.assert_awaited_once_with("p-2", "Inactive")
        graph_repo.set_status.assert_awaited_once_with("p-2", "Inactive")

    async def test_ignores_payload_without_status(self):
        handler, vector_store, graph_repo = _handler()

        await handler.handle({"ProductId": "p-3"})

        vector_store.set_status.assert_not_awaited()
        graph_repo.set_status.assert_not_awaited()
