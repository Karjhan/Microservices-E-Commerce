def build_qdrant_filters(raw_filters: dict) -> dict:
    """
    Convert user-facing filter parameters into Qdrant-compatible filter dict.

    Supported filter keys:
    - category_id: exact match
    - status: exact match
    - materials: list match (any)
    - printers: list match (any)
    - tags: list match (any)
    - price_min / price_max: range
    - min_rating: range
    - attr_*: dynamic facet attribute filters
    """
    qdrant_filters = {}

    if "category_id" in raw_filters:
        qdrant_filters["category_id"] = raw_filters["category_id"]

    if "status" in raw_filters:
        qdrant_filters["status"] = raw_filters["status"]

    if "materials" in raw_filters:
        qdrant_filters["supported_materials"] = raw_filters["materials"]

    if "printers" in raw_filters:
        qdrant_filters["compatible_printers"] = raw_filters["printers"]

    if "tags" in raw_filters:
        qdrant_filters["tags"] = raw_filters["tags"]

    if "price_min" in raw_filters or "price_max" in raw_filters:
        price_filter = {}
        if "price_min" in raw_filters:
            price_filter["gte"] = float(raw_filters["price_min"])
        if "price_max" in raw_filters:
            price_filter["lte"] = float(raw_filters["price_max"])
        qdrant_filters["price"] = price_filter

    if "min_rating" in raw_filters:
        qdrant_filters["average_rating"] = {"gte": float(raw_filters["min_rating"])}

    for key, value in raw_filters.items():
        if key.startswith("attr_"):
            qdrant_filters[key] = value

    return qdrant_filters
