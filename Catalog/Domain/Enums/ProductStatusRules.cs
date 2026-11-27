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
}
