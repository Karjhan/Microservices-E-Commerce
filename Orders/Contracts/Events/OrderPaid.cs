namespace Contracts.Events;

public class OrderPaid
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = default!;
    public DateTime PaidAt { get; init; }
}
