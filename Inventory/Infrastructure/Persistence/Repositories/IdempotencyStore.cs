using Application.Abstractions.Persistence;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class IdempotencyStore : IIdempotencyStore
{
    private readonly InventoryDbContext _context;

    public IdempotencyStore(InventoryDbContext context)
    {
        _context = context;
    }

    public Task<bool> HasProcessedAsync(string consumerName, string idempotencyKey, CancellationToken ct)
    {
        return _context.ProcessedIntegrationEvents
            .AsNoTracking()
            .AnyAsync(
                x => x.ConsumerName == consumerName && x.IdempotencyKey == idempotencyKey,
                ct);
    }

    public async Task MarkProcessedAsync(string consumerName, string idempotencyKey, CancellationToken ct)
    {
        var entry = new ProcessedIntegrationEvent(consumerName, idempotencyKey);

        await _context.ProcessedIntegrationEvents.AddAsync(entry, ct);
    }
}
