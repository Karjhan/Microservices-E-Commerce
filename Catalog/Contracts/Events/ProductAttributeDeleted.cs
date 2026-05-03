namespace Contracts.Events;

public class ProductAttributeDeleted
{
    public Guid ProductId { get; init; }
    public Guid AttributeId { get; init; }

    public string Key { get; init; } = default!;
}