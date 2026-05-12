namespace Contracts.Events;

public class PaymentCompleted
{
    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }

    public decimal Amount { get; init; }
    public string Currency { get; init; } = default!;

    public string ProviderPaymentId { get; init; } = default!;

    public DateTime CompletedAt { get; init; }
}
