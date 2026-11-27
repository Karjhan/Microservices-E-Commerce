namespace Domain.Enums;

public static class ProductStatusRules
{
    private static readonly IReadOnlyDictionary<ProductStatus, ProductStatus[]> Allowed =
        new Dictionary<ProductStatus, ProductStatus[]>
        {
            [ProductStatus.Draft] = [ProductStatus.Active, ProductStatus.Discontinued],
            [ProductStatus.Active] =
                [ProductStatus.Draft, ProductStatus.Inactive, ProductStatus.OutOfStock, ProductStatus.Discontinued],
            [ProductStatus.Inactive] = [ProductStatus.Active, ProductStatus.Discontinued],
            [ProductStatus.OutOfStock] = [ProductStatus.Active, ProductStatus.Inactive, ProductStatus.Discontinued],
            [ProductStatus.Discontinued] = []
        };

    public static bool CanTransition(ProductStatus from, ProductStatus to) =>
        from == to || Allowed[from].Contains(to);

    public static IReadOnlyList<ProductStatus> NextStatuses(ProductStatus from) => Allowed[from];

    /// <summary>
    /// The status a product moves to when its available stock changes, or null when it stays as it is. Only an
    /// <c>Active</c> product sells out and only an <c>OutOfStock</c> one comes back: a draft, an inactive or a
    /// discontinued product is left alone because an admin chose that.
    /// </summary>
    public static ProductStatus? StatusAfterStockChange(ProductStatus current, int availableQuantity) => current switch
    {
        ProductStatus.Active when availableQuantity <= 0 => ProductStatus.OutOfStock,
        ProductStatus.OutOfStock when availableQuantity > 0 => ProductStatus.Active,
        _ => null
    };
}
