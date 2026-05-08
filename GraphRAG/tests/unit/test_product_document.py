"""Unit tests for product document model."""

import pytest
from app.domain.product_document import (
    ProductDocument,
    ProductAttribute,
    AttributeBucket,
)


class TestProductDocument:
    def test_to_embedding_text_includes_name_and_descriptions(self):
        doc = ProductDocument(
            id="test-id",
            name="Articulated Dragon",
            slug="articulated-dragon",
            short_description="A cool dragon",
            long_description="A very detailed articulated dragon model",
            price=4.99,
            currency="USD",
            category_id="cat-1",
            status="active",
        )

        text = doc.to_embedding_text()
        assert "Articulated Dragon" in text
        assert "A cool dragon" in text
        assert "A very detailed articulated dragon model" in text

    def test_to_embedding_text_includes_tags(self):
        doc = ProductDocument(
            id="test-id",
            name="Test",
            slug="test",
            short_description="desc",
            long_description="long desc",
            price=1.0,
            currency="USD",
            category_id="cat-1",
            status="active",
            tags=["dragon", "articulated"],
        )

        text = doc.to_embedding_text()
        assert "dragon" in text
        assert "articulated" in text

    def test_to_embedding_text_includes_semantic_attributes(self):
        doc = ProductDocument(
            id="test-id",
            name="Test",
            slug="test",
            short_description="desc",
            long_description="long desc",
            price=1.0,
            currency="USD",
            category_id="cat-1",
            status="active",
            attributes=[
                ProductAttribute(key="eco-friendly", value="yes", bucket=AttributeBucket.SEMANTIC),
                ProductAttribute(key="color", value="red", bucket=AttributeBucket.FACET),
            ],
        )

        text = doc.to_embedding_text()
        assert "eco-friendly" in text
        assert "Characteristics" in text

    def test_to_embedding_text_excludes_operational_attributes(self):
        doc = ProductDocument(
            id="test-id",
            name="Test",
            slug="test",
            short_description="desc",
            long_description="long desc",
            price=1.0,
            currency="USD",
            category_id="cat-1",
            status="active",
            attributes=[
                ProductAttribute(
                    key="download_count", value="1000", bucket=AttributeBucket.OPERATIONAL
                ),
            ],
            download_count=1000,
        )

        text = doc.to_embedding_text()
        # Operational attributes should NOT be in embedding text
        assert "1000" not in text

    def test_to_qdrant_payload_includes_facet_attributes(self):
        doc = ProductDocument(
            id="test-id",
            name="Test",
            slug="test",
            short_description="desc",
            long_description="long desc",
            price=9.99,
            currency="USD",
            category_id="cat-1",
            status="active",
            tags=["tag1"],
            supported_materials=["PLA"],
            compatible_printers=["Ender 3"],
            attributes=[
                ProductAttribute(key="color", value="red", bucket=AttributeBucket.FACET),
                ProductAttribute(key="eco-friendly", value="yes", bucket=AttributeBucket.SEMANTIC),
            ],
        )

        payload = doc.to_qdrant_payload()
        assert payload["product_id"] == "test-id"
        assert payload["price"] == 9.99
        assert payload["tags"] == ["tag1"]
        assert payload["supported_materials"] == ["PLA"]
        assert payload["attr_color"] == "red"
        # Semantic attributes are NOT in payload as attr_ fields
        assert "attr_eco-friendly" not in payload
