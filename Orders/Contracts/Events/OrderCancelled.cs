namespace Contracts.Events;

public class OrderCancelled
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public string Reason { get; init; } = default!;
    public DateTime CancelledAt { get; init; }
}
