using Infrastructure.Persistence;
using Shared.Security.Audit;

namespace Infrastructure.Audit;

/// <summary>
/// Stores audit entries in the Payments database. <see cref="WriteAsync"/> only stages the entry so it is
/// committed by the same SaveChanges as the change it describes.
/// </summary>
public class EfAuditWriter : IAuditWriter
{
    private readonly PaymentsDbContext _db;

    public EfAuditWriter(PaymentsDbContext db)
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