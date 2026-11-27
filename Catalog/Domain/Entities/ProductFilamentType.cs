namespace Domain.Entities;

/// <summary>Join row between a product and one of its filament types.</summary>
public class ProductFilamentType
{
    public Guid ProductId { get; private set; }
    public Guid FilamentTypeId { get; private set; }

    public Product Product { get; private set; } = null!;
    public FilamentType FilamentType { get; private set; } = null!;

    private ProductFilamentType() { }

    public ProductFilamentType(Guid productId, Guid filamentTypeId)
    {
        ProductId = productId;
        FilamentTypeId = filamentTypeId;
    }
}
