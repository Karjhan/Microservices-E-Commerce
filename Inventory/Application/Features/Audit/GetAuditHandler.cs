using Application.Abstractions.Persistence;
using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Audit;

public class GetAuditHandler(IAuditReader reader) : IRequestHandler<GetAuditQuery, AuditPage>
{
    /// <summary>Service-local audit entries, newest first.</summary>
    public Task<AuditPage> Handle(GetAuditQuery request, CancellationToken ct)
    {
        var (page, pageSize) = AuditPaging.Normalize(request.Page, request.PageSize);
        return reader.GetPageAsync(page, pageSize, ct);
    }
}