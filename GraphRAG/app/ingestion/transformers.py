"""
Transforms raw event payloads into canonical ProductDocument instances.
Event → Transformer → ProductDocument
"""

from app.domain.product_document import ProductDocument, ProductAttribute
from app.ingestion.attribute_classifier import classify_attribute


def transform_product_created(payload: dict) -> ProductDocument:
    """Transform a ProductCreated event payload into a ProductDocument."""
    attributes = [
        ProductAttribute(
            key=attr["Key"],
            value=attr["Value"],
            bucket=classify_attribute(attr["Key"]),
        )
        for attr in payload.get("Attributes", [])
    ]

    return ProductDocument(
        id=payload["ProductId"],
        name=payload["Name"],
        slug=payload["Slug"],
        short_description=payload["ShortDescription"],
        long_description=payload["LongDescription"],
        price=float(payload["Price"]),
        currency=payload["Currency"],
        category_id=payload["CategoryId"],
        status=payload["Status"],
        tags=payload.get("Tags", []),
        supported_materials=payload.get("SupportedMaterials", []),
        compatible_printers=payload.get("CompatiblePrinters", []),
        attributes=attributes,
        download_count=payload.get("DownloadCount", 0),
        average_rating=payload.get("AverageRating", 0.0),
    )


def transform_product_updated(payload: dict) -> ProductDocument:
    """Transform a ProductUpdated event payload into a ProductDocument."""
    return ProductDocument(
        id=payload["ProductId"],
        name=payload["Name"],
        slug=payload["Slug"],
        short_description=payload["ShortDescription"],
        long_description=payload["LongDescription"],
        price=float(payload["Price"]),
        currency=payload["Currency"],
        category_id="",  # ProductUpdated doesn't include CategoryId
        status=payload["Status"],
        tags=payload.get("Tags", []),
        supported_materials=payload.get("SupportedMaterials", []),
        compatible_printers=payload.get("CompatiblePrinters", []),
        attributes=[],
        download_count=payload.get("DownloadCount", 0),
        average_rating=payload.get("AverageRating", 0.0),
    )
