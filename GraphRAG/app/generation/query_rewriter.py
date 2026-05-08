from app.core.logging import get_logger
from app.generation.llm_client import LLMClient
from pathlib import Path

logger = get_logger(__name__)

PROMPTS_DIR = Path(__file__).parent.parent.parent / "prompts"

class QueryRewriter:
    """Rewrites user queries for better retrieval performance."""

    def __init__(self, llm_client: LLMClient) -> None:
        self._llm = llm_client
        self._system_prompt = self._load_prompt("query_rewrite.txt")

    def _load_prompt(self, filename: str) -> str:
        prompt_path = PROMPTS_DIR / filename
        if prompt_path.exists():
            return prompt_path.read_text(encoding="utf-8")
        return self._default_system_prompt()

    def _default_system_prompt(self) -> str:
        return """You are a query rewriter for a 3D printing product catalog search engine.
Your job is to rewrite the user's query to improve retrieval quality.

Rules:
- Expand abbreviations (PLA → polylactic acid filament)
- Add relevant synonyms inline
- Remove filler words and noise
- Keep the core intent intact
- Do NOT add information that wasn't implied by the query
- Do NOT invent product names or brands
- Output ONLY the rewritten query, nothing else

Examples:
Input: "cool dragon model for ender 3"
Output: "dragon figurine model compatible with Creality Ender 3 3D printer"

Input: "cheap pla stuff"
Output: "affordable PLA filament compatible 3D print models"
"""

    async def rewrite(self, query: str) -> str:
        """Rewrite a query for better retrieval."""
        if len(query.split()) <= 2:
            return query

        response = await self._llm.generate_structured(
            prompt=f"Rewrite this search query: {query}",
            system_prompt=self._system_prompt,
        )

        rewritten = response.strip().strip('"').strip("'")

        if not rewritten or len(rewritten) > len(query) * 5:
            logger.warning("Query rewriter returned suspicious output, using original")
            return query

        logger.info("Query rewritten", original=query, rewritten=rewritten)
        return rewritten
