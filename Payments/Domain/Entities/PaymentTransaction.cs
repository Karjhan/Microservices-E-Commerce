using Domain.Enums;

namespace Domain.Entities;

public class PaymentTransaction
{
    public Guid Id { get; private set; }

    public Guid PaymentId { get; private set; }

    public TransactionType Type { get; private set; }

    public decimal Amount { get; private set; }

    public string? ProviderTransactionId { get; private set; }

    public bool Success { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Payment Payment { get; private set; } = null!;

    private PaymentTransaction() { }

    public PaymentTransaction(
        Guid paymentId,
        TransactionType type,
        decimal amount,
        string? providerTransactionId,
        bool success,
        string? failureReason = null)
    {
        Id = Guid.NewGuid();
        PaymentId = paymentId;
        Type = type;
        Amount = amount;
        ProviderTransactionId = providerTransactionId;
        Success = success;
        FailureReason = failureReason;
        CreatedAt = DateTime.UtcNow;
    }
}
