namespace Shared.Security.Audit;

public interface IAuditedRequest
{
    string AuditAction { get; }

    string AuditTargetType { get; }

    string? AuditTargetId { get; }

    object? AuditDetails { get; }
}
