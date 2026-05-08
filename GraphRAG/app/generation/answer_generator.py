from app.core.logging import get_logger
from app.domain.retrieval_result import RetrievalResult
from app.generation.llm_client import LLMClient
from pathlib import Path

logger = get_logger(__name__)

PROMPTS_DIR = Path(__file__).parent.parent.parent / "prompts"

class AnswerGenerator:
    """Generates answers from retrieved product context."""

    def __init__(self, llm_client: LLMClient) -> None:
        self._llm = llm_client
        self._system_prompt = self._load_prompt("answer_generation.txt")

    def _load_prompt(self, filename: str) -> str:
        prompt_path = PROMPTS_DIR / filename
        if prompt_path.exists():
            return prompt_path.read_text(encoding="utf-8")
        return self._default_system_prompt()

    def _default_system_prompt(self) -> str:
        return """You are a helpful assistant for a 3D printing product catalog.
Answer the user's question using ONLY the provided product context.

Rules:
- Only state facts present in the context
- If the answer is not in the context, say "I don't have enough information to answer that"
- Do NOT invent product names, materials, compatibility, or specifications
- Do NOT make claims about products that aren't supported by the context
- Be concise and helpful
- When recommending products, explain WHY they match the query
- Include relevant specifications (materials, printers, ratings) when helpful"""

    async def generate_answer(
        self,
        query: str,
        results: list[RetrievalResult],
        max_context_items: int = 5,
    ) -> str:
        """Generate an answer from retrieved results."""
        if not results:
            return "I couldn't find any products matching your query."

        context_parts = []
        for i, result in enumerate(results[:max_context_items], 1):
            parts = [f"{i}. {result.name}"]
            meta = result.metadata
            if meta.get("tags"):
                parts.append(f"   Tags: {', '.join(meta['tags'])}")
            if meta.get("supported_materials"):
                parts.append(f"   Materials: {', '.join(meta['supported_materials'])}")
            if meta.get("compatible_printers"):
                parts.append(f"   Printers: {', '.join(meta['compatible_printers'])}")
            if meta.get("price"):
                parts.append(f"   Price: {meta['price']} {meta.get('currency', '')}")
            if meta.get("average_rating"):
                parts.append(f"   Rating: {meta['average_rating']}/5")
            context_parts.append("\n".join(parts))

        context = "\n\n".join(context_parts)

        prompt = f"""User question: {query}

Product context:
{context}

Answer the user's question based on the above context."""

        answer = await self._llm.generate(
            prompt=prompt,
            system_prompt=self._system_prompt,
            use_quality_model=True, 
            temperature=0.3,
        )

        return answer.strip()
