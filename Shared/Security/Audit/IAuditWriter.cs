namespace Shared.Security.Audit;

public interface IAuditWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    Task WriteStandaloneAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
