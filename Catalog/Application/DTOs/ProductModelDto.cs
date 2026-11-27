using Domain.Entities;

namespace Application.DTOs;

public class ProductModelDto
{
    public Guid ModelId { get; init; }
    public string OriginalFilename { get; init; } = default!;
    public string Format { get; init; } = default!;
    public long SizeBytes { get; init; }
    public bool IsPreviewable { get; init; }
    public DateTime UploadedAt { get; init; }

    /// <summary>Short-lived presigned download link. Only set by the model endpoint, never cached.</summary>
    public string? Url { get; init; }

    public static ProductModelDto FromEntity(ProductModel model, string? url = null) => new()
    {
        ModelId = model.Id,
        OriginalFilename = model.OriginalFilename,
        Format = model.Format,
        SizeBytes = model.SizeBytes,
        IsPreviewable = model.IsPreviewable,
        UploadedAt = model.UploadedAt,
        Url = url
    };
}
