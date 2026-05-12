namespace Contracts.Events;

public class PaymentFailed
{
    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }

    public decimal Amount { get; init; }
    public string Currency { get; init; } = default!;

    public string Reason { get; init; } = default!;

    public DateTime FailedAt { get; init; }
}
