namespace Domain.Entities;

public class ProductImage
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ProductId { get; private set; }

    public string ObjectKey { get; private set; } = default!;
    public string Url { get; private set; } = default!;
    public bool IsPrimary { get; private set; }

    public Product Product { get; private set; } = null!;

    private ProductImage() { }

    public ProductImage(Guid productId, string objectKey, string url, bool isPrimary)
    {
        ProductId = productId;
        ObjectKey = objectKey;
        Url = url;
        IsPrimary = isPrimary;
    }

    public void SetPrimary(bool value) => IsPrimary = value;
}