using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Security.Audit;

namespace Application.Features.Audit;

public class GetAuditHandler
{
    private readonly AuthDbContext _db;

    public GetAuditHandler(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<AuditPage> Handle(GetAuditQuery query, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = AuditPaging.Normalize(query.Page, query.PageSize);

        var total = await _db.AuditEntries.CountAsync(cancellationToken);

        var items = await _db.AuditEntries
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AuditPage(items, page, pageSize, total);
    }
}
