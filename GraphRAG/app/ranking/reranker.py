import torch
from sentence_transformers import CrossEncoder
from app.core.settings import get_settings
from app.core.logging import get_logger
from app.domain.retrieval_result import RetrievalResult

logger = get_logger(__name__)


class Reranker:
    def __init__(self, model_name: str | None = None, use_fp16: bool = True) -> None:
        settings = get_settings()
        resolved_model = model_name or f"BAAI/{settings.reranker_model}"
        device = "cuda" if torch.cuda.is_available() else "cpu"
        logger.info("Loading BGE reranker model", model=resolved_model, device=device)
        self._reranker = CrossEncoder(
            resolved_model,
            max_length=512,
            device=device,
            model_kwargs={"torch_dtype": torch.float16 if use_fp16 and device == "cuda" else torch.float32},
        )
        logger.info("Reranker ready", model=resolved_model, device=device)

    def rerank(
        self,
        query: str,
        candidates: list[RetrievalResult],
        top_k: int = 20,
    ) -> list[RetrievalResult]:
        if not candidates:
            return []

        capped = candidates[:100]
        pairs: list[list[str]] = []
        for c in capped:
            text_parts = [c.name]
            if c.metadata.get("tags"):
                text_parts.append(f"Tags: {', '.join(c.metadata['tags'])}")
            if c.metadata.get("supported_materials"):
                text_parts.append(
                    f"Materials: {', '.join(c.metadata['supported_materials'])}"
                )
            if c.metadata.get("compatible_printers"):
                text_parts.append(
                    f"Printers: {', '.join(c.metadata['compatible_printers'])}"
                )
            pairs.append([query, " | ".join(text_parts)])

        if not pairs:
            return candidates[:top_k]

        scores: list[float] = self._reranker.predict(pairs, convert_to_numpy=True).tolist()

        for candidate, score in zip(capped, scores):
            candidate.rerank_score = float(score)
            candidate.score = 0.35 * candidate.score + 0.65 * candidate.rerank_score

        capped.sort(key=lambda x: x.score, reverse=True)

        results = capped[:top_k]
        logger.info("Reranking completed", input=len(candidates), output=len(results))
        return results
