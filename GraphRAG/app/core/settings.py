from pydantic_settings import BaseSettings
from functools import lru_cache


class Settings(BaseSettings):
    # Qdrant
    qdrant_host: str = "localhost"
    qdrant_port: int = 6333
    qdrant_collection: str = "products"

    # Neo4j
    neo4j_uri: str = "bolt://localhost:7687"
    neo4j_user: str = "neo4j"
    neo4j_password: str = "graphrag_secret"

    # RabbitMQ
    rabbitmq_url: str = "amqp://guest:guest@localhost:5672/"
    rabbitmq_exchange: str = "catalog-exchange"
    rabbitmq_queue: str = "graphrag-ingestion"

    # Ollama
    ollama_base_url: str = "http://localhost:11434"

    # Models
    embedding_model: str = "bge-m3"
    reranker_model: str = "bge-reranker-v2-m3"
    generation_model: str = "gemma3:4b"
    generation_model_large: str = "gemma3:12b"

    # Retrieval
    hybrid_search_limit: int = 50
    graph_expansion_depth: int = 2
    rerank_top_k: int = 20
    final_top_k: int = 10

    # Logging
    log_level: str = "INFO"

    # OpenTelemetry
    otel_endpoint: str = "http://localhost:4317"
    service_name: str = "graphrag-service"
    otel_enabled: bool = True

    model_config = {"env_file": ".env", "extra": "ignore"}


@lru_cache
def get_settings() -> Settings:
    return Settings()
