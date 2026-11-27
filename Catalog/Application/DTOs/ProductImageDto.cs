using Domain.Entities;

namespace Application.DTOs;

public class ProductImageDto
{
    public Guid ImageId { get; init; }
    public string ObjectKey { get; init; } = default!;
    public string Url { get; init; } = default!;
    public bool IsPrimary { get; init; }
    public int SortOrder { get; init; }
    public string ContentType { get; init; } = default!;
    public long SizeBytes { get; init; }

    public static ProductImageDto FromEntity(ProductImage image) => new()
    {
        ImageId = image.Id,
        ObjectKey = image.ObjectKey,
        Url = image.Url,
        IsPrimary = image.IsPrimary,
        SortOrder = image.SortOrder,
        ContentType = image.ContentType,
        SizeBytes = image.SizeBytes
    };
}
