namespace Contracts.Events;

public class PaymentCreated
{
    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }

    public decimal Amount { get; init; }
    public string Currency { get; init; } = default!;

    public string Method { get; init; } = default!;
    public string Status { get; init; } = default!;

    public string IdempotencyKey { get; init; } = default!;

    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}
