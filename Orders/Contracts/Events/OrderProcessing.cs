namespace Contracts.Events;

public class OrderProcessing
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public DateTime ProcessingAt { get; init; }
}
