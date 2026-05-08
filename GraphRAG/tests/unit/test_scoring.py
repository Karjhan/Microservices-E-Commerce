"""Unit tests for business scoring."""

import pytest
from app.ranking.scoring import apply_business_signals
from app.domain.retrieval_result import RetrievalResult


class TestBusinessScoring:
    def test_boosts_high_rated_products(self):
        candidates = [
            RetrievalResult(
                product_id="a",
                name="A",
                slug="a",
                score=0.5,
                metadata={"average_rating": 5.0, "download_count": 100},
            ),
            RetrievalResult(
                product_id="b",
                name="B",
                slug="b",
                score=0.5,
                metadata={"average_rating": 2.0, "download_count": 100},
            ),
        ]

        result = apply_business_signals(candidates)

        # Product A should score higher due to better rating
        assert result[0].product_id == "a"
        assert result[0].score > result[1].score

    def test_boosts_popular_products(self):
        candidates = [
            RetrievalResult(
                product_id="a",
                name="A",
                slug="a",
                score=0.5,
                metadata={"average_rating": 4.0, "download_count": 10000},
            ),
            RetrievalResult(
                product_id="b",
                name="B",
                slug="b",
                score=0.5,
                metadata={"average_rating": 4.0, "download_count": 10},
            ),
        ]

        result = apply_business_signals(candidates)
        assert result[0].product_id == "a"

    def test_empty_candidates(self):
        result = apply_business_signals([])
        assert result == []

    def test_does_not_override_relevance(self):
        candidates = [
            RetrievalResult(
                product_id="a",
                name="A",
                slug="a",
                score=0.9,  # High relevance
                metadata={"average_rating": 1.0, "download_count": 0},
            ),
            RetrievalResult(
                product_id="b",
                name="B",
                slug="b",
                score=0.3,  # Low relevance
                metadata={"average_rating": 5.0, "download_count": 50000},
            ),
        ]

        result = apply_business_signals(candidates)
        # Relevance should still dominate (business signals are lightweight)
        assert result[0].product_id == "a"
