using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Security.Audit;
using Shared.Security.Authorization;

namespace Application.Common;

public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly CatalogDbContext _db;
    private readonly IAuditWriter _audit;
    private readonly ICurrentUser _user;
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    public AuditBehavior(
        CatalogDbContext db,
        IAuditWriter audit,
        ICurrentUser user,
        ILogger<AuditBehavior<TRequest, TResponse>> logger)
    {
        _db = db;
        _audit = audit;
        _user = user;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IAuditedRequest audited)
        {
            return await next(cancellationToken);
        }

        if (_db.Database.CurrentTransaction is not null)
        {
            var nested = await next(cancellationToken);
            await _audit.WriteAsync(AuditEntryBuilder.From(audited, _user, AuditOutcome.Success, nested), cancellationToken);
            return nested;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next(cancellationToken);

            await _audit.WriteAsync(AuditEntryBuilder.From(audited, _user, AuditOutcome.Success, response), cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            await RollbackQuietly(transaction);
            await RecordFailure(audited, ex);
            throw;
        }
    }

    private async Task RollbackQuietly(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rollback after failed admin action did not complete");
        }
    }

    private async Task RecordFailure(IAuditedRequest audited, Exception error)
    {
        try
        {
            _db.ChangeTracker.Clear();

            await _audit.WriteStandaloneAsync(
                AuditEntryBuilder.From(audited, _user, AuditOutcome.Failed, extraDetails: new { error = error.GetType().Name }),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not record failed admin action {Action}", audited.AuditAction);
        }
    }
}
