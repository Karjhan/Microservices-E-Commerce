from app.domain.retrieval_result import RetrievalResult


def merge_results(
    vector_candidates: list[RetrievalResult],
    graph_candidates: list[RetrievalResult],
) -> list[RetrievalResult]:
    """
    Merge candidates from vector search and graph expansion.
    Deduplicates by product_id, keeping the highest scores from each source.
    """
    merged: dict[str, RetrievalResult] = {}

    for candidate in vector_candidates:
        merged[candidate.product_id] = RetrievalResult(
            product_id=candidate.product_id,
            name=candidate.name,
            slug=candidate.slug,
            vector_score=candidate.vector_score,
            graph_score=0.0,
            metadata=candidate.metadata,
            source="merged",
        )

    for candidate in graph_candidates:
        if candidate.product_id in merged:
            merged[candidate.product_id].graph_score = candidate.graph_score
        else:
            merged[candidate.product_id] = RetrievalResult(
                product_id=candidate.product_id,
                name=candidate.name,
                slug=candidate.slug,
                vector_score=0.0,
                graph_score=candidate.graph_score,
                metadata=candidate.metadata,
                source="merged",
            )

    results = list(merged.values())
    for r in results:
        r.score = (0.7 * r.vector_score) + (0.3 * r.graph_score)

    results.sort(key=lambda x: x.score, reverse=True)
    return results
