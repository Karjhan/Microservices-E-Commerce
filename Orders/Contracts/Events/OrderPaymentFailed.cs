namespace Contracts.Events;

public class OrderPaymentFailed
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public string Reason { get; init; } = default!;
    public DateTime FailedAt { get; init; }
}
