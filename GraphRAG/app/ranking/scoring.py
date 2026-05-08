"""
Business scoring signals: boost candidates by operational metrics.
Used alongside semantic and graph scores.
"""

from app.domain.retrieval_result import RetrievalResult


def apply_business_signals(
    candidates: list[RetrievalResult],
    rating_weight: float = 0.1,
    popularity_weight: float = 0.05,
) -> list[RetrievalResult]:
    """
    Apply business boosting signals (rating, download count) to candidates.
    These are lightweight boosts that don't override semantic relevance.
    """
    if not candidates:
        return candidates

    # Normalize download counts for scoring
    max_downloads = max(
        (c.metadata.get("download_count", 0) for c in candidates), default=1
    )
    max_downloads = max(max_downloads, 1)  # Avoid division by zero

    for candidate in candidates:
        rating = candidate.metadata.get("average_rating", 0)
        downloads = candidate.metadata.get("download_count", 0)

        # Normalized boosts (0 to 1 range)
        rating_boost = (rating / 5.0) * rating_weight
        popularity_boost = (downloads / max_downloads) * popularity_weight

        candidate.score += rating_boost + popularity_boost

    # Re-sort after applying business signals
    candidates.sort(key=lambda x: x.score, reverse=True)
    return candidates
