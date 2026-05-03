using Domain.Entities;

namespace Domain.Commons;

public class ProductRelation
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ProductId { get; private set; }

    public Guid RelatedProductId { get; private set; }

    public string Type { get; private set; } = default!;

    public Product Product { get; private set; } = null!;

    private ProductRelation() { }

    public ProductRelation(Guid productId, Guid relatedProductId, string type)
    {
        ProductId = productId;
        RelatedProductId = relatedProductId;
        Type = type;
    }
}