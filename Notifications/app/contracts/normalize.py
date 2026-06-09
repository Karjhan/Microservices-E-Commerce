def _pascal_to_camel(name: str) -> str:
    if not name:
        return name
    return name[0].lower() + name[1:]


def normalize_wire_payload(value):
    if isinstance(value, dict):
        return {_pascal_to_camel(key): normalize_wire_payload(item) for key, item in value.items()}
    if isinstance(value, list):
        return [normalize_wire_payload(item) for item in value]
    return value