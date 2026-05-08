"""Unit tests for the transformer functions."""

import pytest
from app.ingestion.transformers import transform_product_created, transform_product_updated
from app.domain.product_document import AttributeBucket


class TestTransformProductCreated:
    def test_transforms_basic_fields(self):
        payload = {
            "ProductId": "550e8400-e29b-41d4-a716-446655440001",
            "Name": "Test Product",
            "Slug": "test-product",
            "ShortDescription": "Short desc",
            "LongDescription": "Long desc",
            "Price": 9.99,
            "Currency": "USD",
            "CategoryId": "cat-1",
            "Status": "active",
            "Tags": ["tag1", "tag2"],
            "SupportedMaterials": ["PLA", "PETG"],
            "CompatiblePrinters": ["Ender 3"],
            "DownloadCount": 100,
            "AverageRating": 4.5,
            "Attributes": [
                {"Key": "color", "Value": "red"},
                {"Key": "eco-friendly", "Value": "yes"},
            ],
            "CreatedAt": "2024-01-01T00:00:00Z",
        }

        doc = transform_product_created(payload)

        assert doc.id == "550e8400-e29b-41d4-a716-446655440001"
        assert doc.name == "Test Product"
        assert doc.slug == "test-product"
        assert doc.price == 9.99
        assert doc.tags == ["tag1", "tag2"]
        assert doc.supported_materials == ["PLA", "PETG"]
        assert doc.compatible_printers == ["Ender 3"]
        assert doc.download_count == 100
        assert doc.average_rating == 4.5
        assert len(doc.attributes) == 2

    def test_classifies_attributes_correctly(self):
        payload = {
            "ProductId": "id-1",
            "Name": "Test",
            "Slug": "test",
            "ShortDescription": "desc",
            "LongDescription": "long desc",
            "Price": 1.0,
            "Currency": "USD",
            "CategoryId": "cat-1",
            "Status": "active",
            "Tags": [],
            "SupportedMaterials": [],
            "CompatiblePrinters": [],
            "DownloadCount": 0,
            "AverageRating": 0,
            "Attributes": [
                {"Key": "color", "Value": "blue"},
                {"Key": "eco-friendly", "Value": "yes"},
                {"Key": "download_count", "Value": "500"},
            ],
            "CreatedAt": "2024-01-01T00:00:00Z",
        }

        doc = transform_product_created(payload)

        assert doc.attributes[0].bucket == AttributeBucket.FACET  # color
        assert doc.attributes[1].bucket == AttributeBucket.SEMANTIC  # eco-friendly
        assert doc.attributes[2].bucket == AttributeBucket.OPERATIONAL  # download_count


class TestTransformProductUpdated:
    def test_transforms_update_fields(self):
        payload = {
            "ProductId": "id-1",
            "Name": "Updated Product",
            "Slug": "updated-product",
            "ShortDescription": "Updated short",
            "LongDescription": "Updated long",
            "Price": 12.99,
            "Currency": "EUR",
            "Status": "active",
            "Tags": ["new-tag"],
            "SupportedMaterials": ["ABS"],
            "CompatiblePrinters": ["Prusa i3"],
            "DownloadCount": 200,
            "AverageRating": 4.7,
            "UpdatedAt": "2024-06-01T00:00:00Z",
        }

        doc = transform_product_updated(payload)

        assert doc.id == "id-1"
        assert doc.name == "Updated Product"
        assert doc.price == 12.99
        assert doc.currency == "EUR"
        assert doc.tags == ["new-tag"]
