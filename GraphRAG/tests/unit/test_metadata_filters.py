"""Unit tests for metadata filter builder."""

import pytest
from app.retrieval.metadata_filters import build_qdrant_filters


class TestMetadataFilters:
    def test_category_filter(self):
        result = build_qdrant_filters({"category_id": "cat-1"})
        assert result == {"category_id": "cat-1"}

    def test_materials_filter(self):
        result = build_qdrant_filters({"materials": ["PLA", "PETG"]})
        assert result == {"supported_materials": ["PLA", "PETG"]}

    def test_printers_filter(self):
        result = build_qdrant_filters({"printers": ["Ender 3"]})
        assert result == {"compatible_printers": ["Ender 3"]}

    def test_price_range_filter(self):
        result = build_qdrant_filters({"price_min": 5.0, "price_max": 20.0})
        assert result == {"price": {"gte": 5.0, "lte": 20.0}}

    def test_rating_filter(self):
        result = build_qdrant_filters({"min_rating": 4.0})
        assert result == {"average_rating": {"gte": 4.0}}

    def test_dynamic_attribute_filter(self):
        result = build_qdrant_filters({"attr_color": "red", "attr_finish": "matte"})
        assert result == {"attr_color": "red", "attr_finish": "matte"}

    def test_combined_filters(self):
        result = build_qdrant_filters({
            "category_id": "cat-1",
            "materials": ["PLA"],
            "price_min": 3.0,
            "min_rating": 4.0,
        })
        assert result["category_id"] == "cat-1"
        assert result["supported_materials"] == ["PLA"]
        assert result["price"] == {"gte": 3.0}
        assert result["average_rating"] == {"gte": 4.0}

    def test_empty_filters(self):
        result = build_qdrant_filters({})
        assert result == {}
