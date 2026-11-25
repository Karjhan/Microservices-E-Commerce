namespace Shared.Security.Audit;

public sealed record AuditEntry(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? ActorId,
    string? ActorEmail,
    string Action,
    string TargetType,
    string? TargetId,
    string Outcome,
    string Details,
    string? CorrelationId)
{
    public static AuditEntry Create(
        string action,
        string targetType,
        string? targetId,
        string outcome,
        Guid? actorId,
        string? actorEmail,
        string? details = null,
        string? correlationId = null,
        DateTimeOffset? occurredAt = null) =>
        new(
            Guid.NewGuid(),
            occurredAt ?? DateTimeOffset.UtcNow,
            actorId,
            actorEmail,
            action,
            targetType,
            targetId,
            outcome,
            string.IsNullOrWhiteSpace(details) ? "{}" : details,
            correlationId);
}
