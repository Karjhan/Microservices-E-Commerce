namespace Application.Contracts;

public class LineItemV1
{
    public Guid ProductId { get; init; }
    public string Description { get; init; } = default!;
    public int Quantity { get; init; }
    public MoneyV1 UnitPrice { get; init; } = default!;
    public MoneyV1 LineTotal { get; init; } = default!;
}
