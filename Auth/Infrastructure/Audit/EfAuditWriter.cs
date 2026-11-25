using Infrastructure.Persistence;
using Shared.Security.Audit;

namespace Infrastructure.Audit;

public class EfAuditWriter : IAuditWriter
{
    private readonly AuthDbContext _db;

    public EfAuditWriter(AuthDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await _db.AuditEntries.AddAsync(entry, cancellationToken);
    }

    public async Task WriteStandaloneAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await _db.AuditEntries.AddAsync(entry, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
