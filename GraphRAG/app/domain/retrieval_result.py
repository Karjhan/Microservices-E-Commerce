from dataclasses import dataclass, field


@dataclass
class RetrievalResult:
    """A single retrieval candidate with scores from multiple sources."""

    product_id: str
    name: str
    slug: str
    score: float = 0.0
    vector_score: float = 0.0
    graph_score: float = 0.0
    rerank_score: float = 0.0
    metadata: dict = field(default_factory=dict)
    source: str = ""  # "qdrant", "neo4j", "merged"


@dataclass
class SearchResponse:
    """Final response from a search pipeline."""

    results: list[RetrievalResult] = field(default_factory=list)
    total_candidates: int = 0
    intent: str = ""
    answer: str | None = None  # LLM-generated answer for chat mode
