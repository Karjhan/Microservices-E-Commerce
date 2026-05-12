namespace Contracts.Events;

public class PaymentRefunded
{
    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }

    public decimal RefundAmount { get; init; }
    public decimal TotalRefunded { get; init; }
    public decimal OriginalAmount { get; init; }
    public string Currency { get; init; } = default!;

    public bool IsFullRefund { get; init; }

    public DateTime RefundedAt { get; init; }
}
