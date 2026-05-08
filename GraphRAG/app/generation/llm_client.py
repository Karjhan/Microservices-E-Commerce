import ollama
from app.core.settings import get_settings
from app.core.logging import get_logger

logger = get_logger(__name__)


class LLMClient:
    def __init__(self) -> None:
        settings = get_settings()
        self._client = ollama.AsyncClient(host=settings.ollama_base_url)
        self._fast_model = settings.generation_model
        self._quality_model = settings.generation_model_large 

    async def generate(
        self,
        prompt: str,
        system_prompt: str = "",
        use_quality_model: bool = False,
        temperature: float = 0.1,
        max_tokens: int = 1024,
    ) -> str:
        """
        Generate text using Ollama.
        - use_quality_model=False → fast model (rewriting, extraction)
        - use_quality_model=True → quality model (answer synthesis)
        """
        model = self._quality_model if use_quality_model else self._fast_model

        messages = []
        if system_prompt:
            messages.append({"role": "system", "content": system_prompt})
        messages.append({"role": "user", "content": prompt})

        response = await self._client.chat(
            model=model,
            messages=messages,
            options={
                "temperature": temperature,
                "num_predict": max_tokens,
            },
        )

        content = response["message"]["content"]
        logger.debug("LLM generation completed", model=model, tokens=len(content.split()))
        return content

    async def generate_structured(
        self,
        prompt: str,
        system_prompt: str = "",
        temperature: float = 0.0,
    ) -> str:
        """
        Generate with low temperature for structured/extractive output.
        Used for intent classification and query rewriting where
        we want deterministic, constrained output.
        """
        return await self.generate(
            prompt=prompt,
            system_prompt=system_prompt,
            use_quality_model=False,
            temperature=temperature,
            max_tokens=256,
        )
