using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Persistence;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task AddAsync(Document document, CancellationToken cancellationToken = default);
    Task UpdateAsync(Document document, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Document> Items, int TotalCount)> ListAsync(
        Guid? orderId,
        Guid? paymentId,
        DocumentType? documentType,
        GenerationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
