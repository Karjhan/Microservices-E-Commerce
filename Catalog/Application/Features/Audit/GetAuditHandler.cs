using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Security.Audit;

namespace Application.Features.Audit;

public class GetAuditHandler(CatalogDbContext db) : IRequestHandler<GetAuditQuery, AuditPage>
{
    /// <summary>Service-local audit entries, newest first.</summary>
    public async Task<AuditPage> Handle(GetAuditQuery request, CancellationToken ct)
    {
        var (page, pageSize) = AuditPaging.Normalize(request.Page, request.PageSize);

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