from app.domain.product_document import AttributeBucket

FACET_KEYS = frozenset({
    "material",
    "printer_type",
    "size",
    "color",
    "finish",
    "compatibility",
    "weight",
    "dimensions",
    "layer_height",
    "nozzle_size",
    "infill",
    "print_speed",
    "bed_temperature",
    "nozzle_temperature",
    "resolution",
    "filament_diameter",
})

SEMANTIC_KEYS = frozenset({
    "eco_friendly",
    "lightweight",
    "modular",
    "customizable",
    "desk_friendly",
    "beginner_friendly",
    "high_detail",
    "snap_fit",
    "articulated",
    "multipart",
    "functional",
    "decorative",
    "miniature",
    "large_format",
    "watertight",
})

OPERATIONAL_KEYS = frozenset({
    "download_count",
    "average_rating",
    "stock",
    "status",
    "view_count",
    "favorite_count",
    "print_success_rate",
})


def classify_attribute(key: str) -> AttributeBucket:
    normalized = key.lower().replace(" ", "_").replace("-", "_")

    if normalized in FACET_KEYS:
        return AttributeBucket.FACET
    elif normalized in SEMANTIC_KEYS:
        return AttributeBucket.SEMANTIC
    elif normalized in OPERATIONAL_KEYS:
        return AttributeBucket.OPERATIONAL

    return AttributeBucket.FACET
