using Domain.Entities;

namespace Application.DTOs;

public class PaymentDto
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }

    public decimal Amount { get; init; }
    public string Currency { get; init; } = default!;

    public string Status { get; init; } = default!;
    public string Method { get; init; } = default!;

    public string IdempotencyKey { get; init; } = default!;
    public string? ProviderPaymentId { get; init; }
    public string? FailureReason { get; init; }

    public decimal RefundedAmount { get; init; }

    public List<PaymentTransactionDto> Transactions { get; init; } = new();

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime ExpiresAt { get; init; }

    public static PaymentDto FromEntity(Payment payment)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            OrderId = payment.OrderId,
            UserId = payment.UserId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status.ToString(),
            Method = payment.Method.ToString(),
            IdempotencyKey = payment.IdempotencyKey,
            ProviderPaymentId = payment.ProviderPaymentId,
            FailureReason = payment.FailureReason,
            RefundedAmount = payment.RefundedAmount,
            Transactions = payment.Transactions.Select(t => new PaymentTransactionDto
            {
                Id = t.Id,
                Type = t.Type.ToString(),
                Amount = t.Amount,
                ProviderTransactionId = t.ProviderTransactionId,
                Success = t.Success,
                FailureReason = t.FailureReason,
                CreatedAt = t.CreatedAt
            }).ToList(),
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt,
            CompletedAt = payment.CompletedAt,
            ExpiresAt = payment.ExpiresAt
        };
    }
}

public class PaymentTransactionDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = default!;
    public decimal Amount { get; init; }
    public string? ProviderTransactionId { get; init; }
    public bool Success { get; init; }
    public string? FailureReason { get; init; }
    public DateTime CreatedAt { get; init; }
}
