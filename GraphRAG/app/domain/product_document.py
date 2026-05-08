from dataclasses import dataclass, field
from enum import Enum


class AttributeBucket(str, Enum):
    """Classification of attributes into three buckets for storage strategy."""

    FACET = "facet"  # material, printer type, size, color, finish, compatibility
    SEMANTIC = "semantic"  # eco-friendly, lightweight, modular, customizable
    OPERATIONAL = "operational"  # downloadCount, averageRating, stock, status


@dataclass
class ProductAttribute:
    key: str
    value: str
    bucket: AttributeBucket = AttributeBucket.FACET


@dataclass
class ProductDocument:
    """
    Internal canonical representation of a product for GraphRAG.
    NOT a DB model. NOT an event contract.
    This is the GraphRAG-native document used for embedding and indexing.
    """

    id: str
    name: str
    slug: str
    short_description: str
    long_description: str
    price: float
    currency: str
    category_id: str
    status: str
    tags: list[str] = field(default_factory=list)
    supported_materials: list[str] = field(default_factory=list)
    compatible_printers: list[str] = field(default_factory=list)
    attributes: list[ProductAttribute] = field(default_factory=list)
    download_count: int = 0
    average_rating: float = 0.0

    def to_embedding_text(self) -> str:
        """
        Build the canonical text for embedding.
        Includes: name, descriptions, tags, materials, printers, semantic attributes.
        Excludes: raw IDs, timestamps, counters, operational attributes.
        """
        parts = [
            self.name,
            self.short_description,
            self.long_description,
        ]

        if self.tags:
            parts.append(f"Tags: {', '.join(self.tags)}")

        if self.supported_materials:
            parts.append(f"Materials: {', '.join(self.supported_materials)}")

        if self.compatible_printers:
            parts.append(f"Compatible printers: {', '.join(self.compatible_printers)}")

        # Include semantic attributes in embedding text
        semantic_attrs = [
            a for a in self.attributes if a.bucket == AttributeBucket.SEMANTIC
        ]
        if semantic_attrs:
            attr_text = ", ".join(f"{a.key}: {a.value}" for a in semantic_attrs)
            parts.append(f"Characteristics: {attr_text}")

        # Include facet attributes as structured text
        facet_attrs = [a for a in self.attributes if a.bucket == AttributeBucket.FACET]
        if facet_attrs:
            attr_text = ", ".join(f"{a.key}: {a.value}" for a in facet_attrs)
            parts.append(f"Specifications: {attr_text}")

        return "\n".join(parts)

    def to_qdrant_payload(self) -> dict:
        """
        Build the payload for Qdrant point.
        Includes facet attributes for filtering and operational attributes for boosting.
        """
        payload = {
            "product_id": self.id,
            "name": self.name,
            "slug": self.slug,
            "price": self.price,
            "currency": self.currency,
            "category_id": self.category_id,
            "status": self.status,
            "tags": self.tags,
            "supported_materials": self.supported_materials,
            "compatible_printers": self.compatible_printers,
            "download_count": self.download_count,
            "average_rating": self.average_rating,
        }

        # Add facet attributes as individual payload fields
        for attr in self.attributes:
            if attr.bucket == AttributeBucket.FACET:
                payload[f"attr_{attr.key}"] = attr.value

        return payload
