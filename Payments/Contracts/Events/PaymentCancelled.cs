namespace Contracts.Events;

public class PaymentCancelled
{
    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }

    public string Reason { get; init; } = default!;

    public DateTime CancelledAt { get; init; }
}
