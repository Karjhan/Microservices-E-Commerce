"""
conftest.py — shared pytest fixtures.
"""

import pytest


@pytest.fixture
def sample_product_payload() -> dict:
    """A sample ProductCreated event payload for testing."""
    return {
        "ProductId": "550e8400-e29b-41d4-a716-446655440001",
        "Name": "Articulated Dragon",
        "Slug": "articulated-dragon",
        "ShortDescription": "A fully articulated dragon with snap-fit joints",
        "LongDescription": "Beautiful articulated dragon model with 20+ joints.",
        "Price": 4.99,
        "Currency": "USD",
        "CategoryId": "cat-figurines",
        "Status": "active",
        "Tags": ["dragon", "articulated", "print-in-place"],
        "SupportedMaterials": ["PLA", "PETG"],
        "CompatiblePrinters": ["Creality Ender 3", "Prusa i3 MK3S"],
        "DownloadCount": 15420,
        "AverageRating": 4.8,
        "Attributes": [
            {"Key": "finish", "Value": "smooth"},
            {"Key": "eco-friendly", "Value": "yes"},
        ],
        "CreatedAt": "2024-01-15T10:30:00Z",
    }


@pytest.fixture
def sample_updated_payload() -> dict:
    """A sample ProductUpdated event payload."""
    return {
        "ProductId": "550e8400-e29b-41d4-a716-446655440001",
        "Name": "Articulated Dragon V2",
        "Slug": "articulated-dragon-v2",
        "ShortDescription": "Updated articulated dragon",
        "LongDescription": "Updated description.",
        "Price": 5.99,
        "Currency": "USD",
        "Status": "active",
        "Tags": ["dragon", "articulated", "print-in-place", "v2"],
        "SupportedMaterials": ["PLA", "PETG", "ABS"],
        "CompatiblePrinters": ["Creality Ender 3", "Prusa i3 MK3S", "Bambu Lab P1S"],
        "DownloadCount": 16000,
        "AverageRating": 4.9,
        "UpdatedAt": "2024-06-01T12:00:00Z",
    }
