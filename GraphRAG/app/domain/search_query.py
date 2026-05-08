from dataclasses import dataclass, field
from enum import Enum


class SearchIntent(str, Enum):
    """Query intent classification."""

    SEMANTIC_BROWSE = "semantic_browse" 
    COMPATIBILITY_LOOKUP = "compatibility_lookup" 
    RELATION_LOOKUP = "relation_lookup" 
    RECOMMENDATION = "recommendation"
    CHATBOT_QA = "chatbot_qa" 


@dataclass
class SearchQuery:
    raw_query: str
    rewritten_query: str = ""
    intent: SearchIntent = SearchIntent.SEMANTIC_BROWSE
    filters: dict = field(default_factory=dict)
    seed_product_id: str | None = None
    limit: int = 10

    @property
    def effective_query(self) -> str:
        return self.rewritten_query or self.raw_query
