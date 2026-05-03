namespace Contracts.Events;

public class ProductAttributeAdded
{
    public Guid ProductId { get; init; }
    public Guid AttributeId { get; init; }

    public string Key { get; init; } = default!;
    public string Value { get; init; } = default!;
}