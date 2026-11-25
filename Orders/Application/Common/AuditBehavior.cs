using Application.Abstractions.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Security.Audit;
using Shared.Security.Authorization;

namespace Application.Common;

/// <summary>
/// Records every <see cref="IAuditedRequest"/> in the same database transaction as the change itself
/// (FR-007, research R5). On failure the change is rolled back and a <c>failed</c> entry is written on its own.
/// </summary>
public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IOrderRepository _repo;
    private readonly IAuditWriter _audit;
    private readonly ICurrentUser _user;
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    public AuditBehavior(
        IOrderRepository repo,
        IAuditWriter audit,
        ICurrentUser user,
        ILogger<AuditBehavior<TRequest, TResponse>> logger)
    {
        _repo = repo;
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

        await using var transaction = await _repo.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next(cancellationToken);

            await _audit.WriteAsync(AuditEntryBuilder.From(audited, _user, AuditOutcome.Success, response), cancellationToken);
            await _repo.SaveChangesAsync(cancellationToken);
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

    private async Task RollbackQuietly(ITransactionScope transaction)
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
            // Drop anything the failed handler staged so the standalone save writes only the audit entry.
            _repo.DiscardPendingChanges();

            await _audit.WriteStandaloneAsync(
                AuditEntryBuilder.From(audited, _user, AuditOutcome.Failed, extraDetails: new { error = error.GetType().Name }),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Never mask the original failure.
            _logger.LogError(ex, "Could not record failed admin action {Action}", audited.AuditAction);
        }
    }
}