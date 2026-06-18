using Application.Abstractions.Persistence;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly PdfWorkerDbContext _context;

    public DocumentRepository(PdfWorkerDbContext context)
    {
        _context = context;
    }

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Documents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Document?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        _context.Documents.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task AddAsync(Document document, CancellationToken cancellationToken = default) =>
        await _context.Documents.AddAsync(document, cancellationToken);

    public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        _context.Documents.Update(document);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public async Task<(IReadOnlyList<Document> Items, int TotalCount)> ListAsync(
        Guid? orderId,
        Guid? paymentId,
        DocumentType? documentType,
        GenerationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Documents.AsQueryable();

        if (orderId.HasValue)
            query = query.Where(x => x.OrderId == orderId.Value);

        if (paymentId.HasValue)
            query = query.Where(x => x.PaymentId == paymentId.Value);

        if (documentType.HasValue)
            query = query.Where(x => x.DocumentType == documentType.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
