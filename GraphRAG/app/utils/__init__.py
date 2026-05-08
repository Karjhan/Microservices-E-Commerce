def normalize_uuid(raw_id: str) -> str:
    return raw_id.strip().lower().replace("{", "").replace("}", "")


def truncate_text(text: str, max_length: int = 8192) -> str:
    # Rough approximation: 1 token ≈ 4 characters for English text
    char_limit = max_length * 4
    if len(text) <= char_limit:
        return text
    return text[:char_limit]


def clean_text(text: str) -> str:
    if not text:
        return ""
    lines = text.strip().split("\n")
    cleaned_lines = [line.strip() for line in lines if line.strip()]
    return "\n".join(cleaned_lines)
