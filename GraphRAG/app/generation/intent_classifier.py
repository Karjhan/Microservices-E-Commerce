from app.core.logging import get_logger
from app.domain.search_query import SearchIntent
from app.generation.llm_client import LLMClient
from pathlib import Path

logger = get_logger(__name__)

PROMPTS_DIR = Path(__file__).parent.parent.parent / "prompts"


class IntentClassifier:
    """Classifies user queries into search intents for routing."""

    def __init__(self, llm_client: LLMClient) -> None:
        self._llm = llm_client
        self._system_prompt = self._load_prompt("intent_classification.txt")

    def _load_prompt(self, filename: str) -> str:
        prompt_path = PROMPTS_DIR / filename
        if prompt_path.exists():
            return prompt_path.read_text(encoding="utf-8")
        return self._default_system_prompt()

    def _default_system_prompt(self) -> str:
        return """You are a query intent classifier for a 3D printing product catalog.
Classify the user query into exactly ONE of these categories:
- semantic_browse: fuzzy search, general product discovery
- compatibility_lookup: specific printer/material compatibility questions
- relation_lookup: finding related products, accessories, parts
- recommendation: "products like X", "similar to", suggestions
- chatbot_qa: general questions about products, comparisons, advice

Respond with ONLY the category name, nothing else."""

    async def classify(self, query: str) -> SearchIntent:
        response = await self._llm.generate_structured(
            prompt=f"Classify this query: {query}",
            system_prompt=self._system_prompt,
        )

        response_clean = response.strip().lower().replace(" ", "_")

        try:
            return SearchIntent(response_clean)
        except ValueError:
            logger.warning(
                "Unknown intent from LLM, defaulting to semantic_browse",
                raw_response=response,
            )
            return SearchIntent.SEMANTIC_BROWSE
