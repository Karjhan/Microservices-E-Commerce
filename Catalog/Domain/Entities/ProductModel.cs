namespace Domain.Entities;

public class ProductModel
{
    public const long MaxSizeBytes = 100L * 1024 * 1024;

    public static readonly IReadOnlySet<string> PreviewableFormats =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "glb", "gltf", "stl" };

    public static readonly IReadOnlySet<string> StoredOnlyFormats =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "3mf", "obj" };

    public static IEnumerable<string> AllowedFormats => PreviewableFormats.Concat(StoredOnlyFormats);

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ProductId { get; private set; }

    public string ObjectKey { get; private set; } = default!;
    public string OriginalFilename { get; private set; } = default!;

    public string Format { get; private set; } = default!;

    public bool IsPreviewable { get; private set; }
    public long SizeBytes { get; private set; }
    public DateTime UploadedAt { get; private set; }

    public Product Product { get; private set; } = null!;

    private ProductModel() { }

    public ProductModel(Guid id, Guid productId, string format, string originalFilename, long sizeBytes)
    {
        Id = id;
        ProductId = productId;
        Format = format.ToLowerInvariant();
        OriginalFilename = SanitizeFilename(originalFilename);
        SizeBytes = sizeBytes;
        IsPreviewable = PreviewableFormats.Contains(Format);
        ObjectKey = BuildObjectKey(productId, id, Format);
        UploadedAt = DateTime.UtcNow;
    }

    public static string BuildObjectKey(Guid productId, Guid modelId, string format) =>
        $"products/{productId}/model/{modelId}.{format.ToLowerInvariant()}";

    public static string? FormatOf(string filename)
    {
        var extension = Path.GetExtension(filename);
        return string.IsNullOrEmpty(extension) ? null : extension.TrimStart('.').ToLowerInvariant();
    }

    public static string SanitizeFilename(string filename)
    {
        var name = Path.GetFileName(filename ?? string.Empty);
        var cleaned = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();

        if (cleaned.Length == 0)
            cleaned = "model";

        return cleaned.Length > 200 ? cleaned[^200..] : cleaned;
    }
}
