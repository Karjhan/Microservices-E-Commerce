from pydantic import BaseModel, Field

class SearchRequest(BaseModel):
    query: str = Field(..., min_length=1, max_length=500)
    filters: dict = Field(default_factory=dict)
    limit: int = Field(default=10, ge=1, le=100)


class ProductResult(BaseModel):
    product_id: str
    name: str
    slug: str
    score: float
    vector_score: float = 0.0
    graph_score: float = 0.0
    rerank_score: float = 0.0
    metadata: dict = Field(default_factory=dict)


class SearchResponse(BaseModel):
    results: list[ProductResult]
    total_candidates: int
    intent: str
    answer: str | None = None


class RecommendRequest(BaseModel):
    product_id: str = Field(..., min_length=1)
    limit: int = Field(default=10, ge=1, le=50)


class ChatRequest(BaseModel):
    query: str = Field(..., min_length=1, max_length=1000)
    filters: dict = Field(default_factory=dict)


class ChatResponse(BaseModel):
    answer: str
    sources: list[ProductResult]
    intent: str


class HealthResponse(BaseModel):
    status: str
    qdrant: str
    neo4j: str
    ollama: str
