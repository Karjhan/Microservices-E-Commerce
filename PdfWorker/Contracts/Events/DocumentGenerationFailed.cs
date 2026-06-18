namespace PdfWorker.Contracts.Events;

public class DocumentGenerationFailed
{
    public string DocumentType { get; init; } = default!;
    public string IdempotencyKey { get; init; } = default!;
    public Guid OrderId { get; init; }
    public Guid PaymentId { get; init; }
    public Guid UserId { get; init; }
    public string SourceRoutingKey { get; init; } = default!;
    public string FailureReason { get; init; } = default!;
    public string? CorrelationId { get; init; }
    public DateTime FailedAt { get; init; }
}
