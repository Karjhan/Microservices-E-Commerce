namespace PdfWorker.Contracts.Events;

public class DocumentGenerated
{
    public Guid DocumentId { get; init; }
    public string DocumentType { get; init; } = default!;
    public string IdempotencyKey { get; init; } = default!;
    public string TemplateVersion { get; init; } = default!;
    public Guid OrderId { get; init; }
    public Guid PaymentId { get; init; }
    public Guid UserId { get; init; }
    public string StorageBucket { get; init; } = default!;
    public string StorageObjectKey { get; init; } = default!;
    public string ContentType { get; init; } = "application/pdf";
    public long FileSizeBytes { get; init; }
    public string? CorrelationId { get; init; }
    public DateTime GeneratedAt { get; init; }
}
