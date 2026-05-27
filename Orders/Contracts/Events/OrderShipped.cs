namespace Contracts.Events;

public class OrderShipped
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public DateTime ShippedAt { get; init; }
}
