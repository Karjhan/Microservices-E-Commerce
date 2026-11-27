using Application.Abstractions.Persistence;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Security.Audit;

namespace Infrastructure.Audit;

public class EfAuditReader(PaymentsDbContext db) : IAuditReader
{
    public async Task<AuditPage> GetPageAsync(int page, int pageSize, CancellationToken ct)
    {
        var total = await db.AuditEntries.CountAsync(ct);

        var items = await db.AuditEntries
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new AuditPage(items, page, pageSize, total);
    }
}