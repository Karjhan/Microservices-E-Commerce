namespace Domain.Entities;

public class ProductImage
{
    public const long MaxSizeBytes = 5L * 1024 * 1024;

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ProductId { get; private set; }

    public string ObjectKey { get; private set; } = default!;
    public string Url { get; private set; } = default!;
    public bool IsPrimary { get; private set; }

    public int SortOrder { get; private set; }

    public string ContentType { get; private set; } = "image/jpeg";

    public long SizeBytes { get; private set; }

    public Product Product { get; private set; } = null!;

    private ProductImage() { }

    public ProductImage(Guid productId, string objectKey, string url, bool isPrimary)
        : this(Guid.NewGuid(), productId, objectKey, url, "image/jpeg", 0, 0, isPrimary)
    {
    }

    public ProductImage(
        Guid id,
        Guid productId,
        string objectKey,
        string url,
        string contentType,
        long sizeBytes,
        int sortOrder,
        bool isPrimary)
    {
        Id = id;
        ProductId = productId;
        ObjectKey = objectKey;
        Url = url;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        SortOrder = sortOrder;
        IsPrimary = isPrimary;
    }

    public static string BuildObjectKey(Guid productId, Guid imageId) => $"products/{productId}/images/{imageId}";

    public void SetPrimary(bool value) => IsPrimary = value;

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}
