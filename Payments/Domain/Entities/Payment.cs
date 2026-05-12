using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities;

public class Payment
{
    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }

    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "EUR";

    public PaymentStatus Status { get; private set; }
    public PaymentMethod Method { get; private set; }

    public string IdempotencyKey { get; private set; } = default!;

    public string? PaymentToken { get; private set; }

    public string? ProviderPaymentId { get; private set; }
    public string? FailureReason { get; private set; }

    public decimal RefundedAmount { get; private set; }

    public List<PaymentTransaction> Transactions { get; private set; } = new();

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private Payment() { }

    public Payment(
        Guid orderId,
        Guid userId,
        decimal amount,
        string currency,
        PaymentMethod method,
        string idempotencyKey,
        string? paymentToken = null)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        UserId = userId;
        Amount = amount;
        Currency = currency;
        Method = method;
        IdempotencyKey = idempotencyKey;
        PaymentToken = paymentToken;
        Status = PaymentStatus.Pending;
        RefundedAmount = 0;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddMinutes(30);
    }

    public void MarkProcessing()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidPaymentStateException(
                $"Cannot process payment in '{Status}' state. Expected 'Pending'.");

        Status = PaymentStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCompleted(string providerPaymentId)
    {
        if (Status != PaymentStatus.Processing)
            throw new InvalidPaymentStateException(
                $"Cannot complete payment in '{Status}' state. Expected 'Processing'.");

        Status = PaymentStatus.Completed;
        ProviderPaymentId = providerPaymentId;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        if (Status != PaymentStatus.Processing)
            throw new InvalidPaymentStateException(
                $"Cannot fail payment in '{Status}' state. Expected 'Processing'.");

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCancelled()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidPaymentStateException(
                $"Cannot cancel payment in '{Status}' state. Expected 'Pending'.");

        Status = PaymentStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyRefund(decimal refundAmount)
    {
        if (Status != PaymentStatus.Completed && Status != PaymentStatus.PartiallyRefunded)
            throw new InvalidRefundException(
                $"Cannot refund payment in '{Status}' state. Expected 'Completed' or 'PartiallyRefunded'.");

        if (refundAmount <= 0)
            throw new InvalidRefundException("Refund amount must be greater than zero.");

        if (RefundedAmount + refundAmount > Amount)
            throw new InvalidRefundException(
                $"Refund amount {refundAmount} exceeds remaining refundable amount {Amount - RefundedAmount}.");

        RefundedAmount += refundAmount;

        Status = RefundedAmount >= Amount
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;

        UpdatedAt = DateTime.UtcNow;
    }

    public void AddTransaction(PaymentTransaction transaction)
    {
        Transactions.Add(transaction);
    }

    public bool IsExpired => Status == PaymentStatus.Pending && DateTime.UtcNow >= ExpiresAt;
}
