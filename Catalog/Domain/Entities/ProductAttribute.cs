using Domain.Entities;

namespace Domain.Commons;

public class ProductAttribute
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ProductId { get; private set; }

    public string Key { get; private set; } = default!;
    public string Value { get; private set; } = default!;

    public Product Product { get; private set; } = null!;

    private ProductAttribute() { }

    public ProductAttribute(Guid productId, string key, string value)
    {
        ProductId = productId;
        Key = key;
        Value = value;
    }
}