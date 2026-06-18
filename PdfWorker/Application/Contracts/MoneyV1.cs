namespace Application.Contracts;

public class MoneyV1
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = default!;
}
