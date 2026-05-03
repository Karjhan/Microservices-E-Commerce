namespace Contracts.Events;

public class ProductRelationAdded
{
    public Guid ProductId { get; init; }
    public Guid RelationId { get; init; }

    public Guid RelatedProductId { get; init; }

    public string Type { get; init; } = default!;
}