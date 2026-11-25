using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.Security.Audit;

public interface IRefusalAuditRecorder
{
    Task RecordAsync(HttpContext context, string code, CancellationToken cancellationToken = default);
}

public sealed class RefusalAuditRecorder : IRefusalAuditRecorder
{
    public const string Action = "access.denied";
    public const string TargetType = "Route";

    private readonly IAuditWriter? _writer;
    private readonly ILogger<RefusalAuditRecorder> _logger;

    public RefusalAuditRecorder(ILogger<RefusalAuditRecorder> logger, IAuditWriter? writer = null)
    {
        _writer = writer;
        _logger = logger;
    }

    public async Task RecordAsync(HttpContext context, string code, CancellationToken cancellationToken = default)
    {
        if (_writer is null)
        {
            return;
        }

        try
        {
            var user = context.User;
            var signedIn = user.Identity?.IsAuthenticated == true;

            Guid? actorId = null;
            string? email = null;
            if (signedIn)
            {
                var rawId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                actorId = Guid.TryParse(rawId, out var id) ? id : null;
                email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");
            }

            var details = JsonSerializer.Serialize(new
            {
                reason = code,
                status = signedIn ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized
            });

            var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                                ?? context.TraceIdentifier;

            var entry = AuditEntry.Create(
                Action,
                TargetType,
                $"{context.Request.Method} {context.Request.Path}",
                AuditOutcome.Refused,
                actorId,
                email,
                details,
                correlationId);

            await _writer.WriteStandaloneAsync(entry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record access refusal audit entry");
        }
    }
}
