using Domain.Enums;

namespace Domain.Entities;

public class Document
{
    public Guid Id { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public string TemplateVersion { get; private set; } = default!;
    public GenerationStatus Status { get; private set; }
    public string? StorageObjectKey { get; private set; }
    public string? StorageBucket { get; private set; }
    public string? ContentType { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid UserId { get; private set; }
    public string? CorrelationId { get; private set; }
    public string SourceRoutingKey { get; private set; } = default!;
    public string? FailureReason { get; private set; }
    public string? RequestContractJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? FailedAt { get; private set; }

    private Document() { }

    public static Document CreatePending(
        DocumentType documentType,
        string idempotencyKey,
        string templateVersion,
        Guid orderId,
        Guid paymentId,
        Guid userId,
        string sourceRoutingKey,
        string? correlationId,
        string? requestContractJson)
    {
        return new Document
        {
            Id = Guid.NewGuid(),
            DocumentType = documentType,
            IdempotencyKey = idempotencyKey,
            TemplateVersion = templateVersion,
            Status = GenerationStatus.Pending,
            OrderId = orderId,
            PaymentId = paymentId,
            UserId = userId,
            CorrelationId = correlationId,
            SourceRoutingKey = sourceRoutingKey,
            RequestContractJson = requestContractJson,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkCompleted(string bucket, string objectKey, string contentType, long fileSizeBytes)
    {
        Status = GenerationStatus.Completed;
        StorageBucket = bucket;
        StorageObjectKey = objectKey;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        CompletedAt = DateTime.UtcNow;
        FailureReason = null;
        FailedAt = null;
    }

    public void MarkFailed(string reason)
    {
        Status = GenerationStatus.Failed;
        FailureReason = reason;
        FailedAt = DateTime.UtcNow;
    }
}
