using Shared.Security.Audit;

namespace Application.Abstractions.Persistence;

/// <summary>Reads this service's audit entries, newest first.</summary>
public interface IAuditReader
{
    Task<AuditPage> GetPageAsync(int page, int pageSize, CancellationToken ct);
}