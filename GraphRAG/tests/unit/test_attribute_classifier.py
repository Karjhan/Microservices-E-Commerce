"""Unit tests for attribute classifier."""

import pytest
from app.ingestion.attribute_classifier import classify_attribute
from app.domain.product_document import AttributeBucket


class TestAttributeClassifier:
    def test_facet_attributes(self):
        assert classify_attribute("material") == AttributeBucket.FACET
        assert classify_attribute("color") == AttributeBucket.FACET
        assert classify_attribute("size") == AttributeBucket.FACET
        assert classify_attribute("finish") == AttributeBucket.FACET
        assert classify_attribute("nozzle_size") == AttributeBucket.FACET
        assert classify_attribute("layer_height") == AttributeBucket.FACET

    def test_semantic_attributes(self):
        assert classify_attribute("eco-friendly") == AttributeBucket.SEMANTIC
        assert classify_attribute("lightweight") == AttributeBucket.SEMANTIC
        assert classify_attribute("modular") == AttributeBucket.SEMANTIC
        assert classify_attribute("customizable") == AttributeBucket.SEMANTIC
        assert classify_attribute("beginner-friendly") == AttributeBucket.SEMANTIC

    def test_operational_attributes(self):
        assert classify_attribute("download_count") == AttributeBucket.OPERATIONAL
        assert classify_attribute("average_rating") == AttributeBucket.OPERATIONAL
        assert classify_attribute("stock") == AttributeBucket.OPERATIONAL
        assert classify_attribute("status") == AttributeBucket.OPERATIONAL

    def test_unknown_defaults_to_facet(self):
        assert classify_attribute("unknown_attr") == AttributeBucket.FACET
        assert classify_attribute("some_random_thing") == AttributeBucket.FACET

    def test_normalization(self):
        # Handles various formats
        assert classify_attribute("Eco-Friendly") == AttributeBucket.SEMANTIC
        assert classify_attribute("eco_friendly") == AttributeBucket.SEMANTIC
        assert classify_attribute("MATERIAL") == AttributeBucket.FACET
