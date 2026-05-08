"""Unit tests for result merger."""

import pytest
from app.retrieval.result_merger import merge_results
from app.domain.retrieval_result import RetrievalResult


class TestResultMerger:
    def test_merges_non_overlapping_results(self):
        vector = [
            RetrievalResult(product_id="a", name="A", slug="a", vector_score=0.9, source="qdrant"),
            RetrievalResult(product_id="b", name="B", slug="b", vector_score=0.8, source="qdrant"),
        ]
        graph = [
            RetrievalResult(product_id="c", name="C", slug="c", graph_score=0.7, source="neo4j"),
        ]

        merged = merge_results(vector, graph)

        assert len(merged) == 3
        ids = [r.product_id for r in merged]
        assert "a" in ids
        assert "b" in ids
        assert "c" in ids

    def test_deduplicates_overlapping_results(self):
        vector = [
            RetrievalResult(product_id="a", name="A", slug="a", vector_score=0.9, source="qdrant"),
        ]
        graph = [
            RetrievalResult(product_id="a", name="A", slug="a", graph_score=0.6, source="neo4j"),
        ]

        merged = merge_results(vector, graph)

        assert len(merged) == 1
        assert merged[0].product_id == "a"
        assert merged[0].vector_score == 0.9
        assert merged[0].graph_score == 0.6

    def test_combined_score_weights(self):
        vector = [
            RetrievalResult(product_id="a", name="A", slug="a", vector_score=1.0, source="qdrant"),
        ]
        graph = [
            RetrievalResult(product_id="a", name="A", slug="a", graph_score=1.0, source="neo4j"),
        ]

        merged = merge_results(vector, graph)

        # Score = 0.7 * vector + 0.3 * graph = 0.7 + 0.3 = 1.0
        assert merged[0].score == pytest.approx(1.0)

    def test_sorts_by_combined_score(self):
        vector = [
            RetrievalResult(product_id="a", name="A", slug="a", vector_score=0.5, source="qdrant"),
            RetrievalResult(product_id="b", name="B", slug="b", vector_score=0.9, source="qdrant"),
        ]
        graph = []

        merged = merge_results(vector, graph)

        assert merged[0].product_id == "b"  # Higher vector score
        assert merged[1].product_id == "a"

    def test_empty_inputs(self):
        assert merge_results([], []) == []
