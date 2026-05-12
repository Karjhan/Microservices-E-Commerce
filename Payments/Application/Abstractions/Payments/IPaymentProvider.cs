namespace Application.Abstractions.Payments;

public interface IPaymentProvider
{
    Task<ProviderChargeResult> ChargeAsync(
        string paymentToken,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<ProviderRefundResult> RefundAsync(
        string providerPaymentId,
        decimal amount,
        CancellationToken ct = default);
}

public record ProviderChargeResult(bool Success, string? ProviderPaymentId, string? FailureReason);

public record ProviderRefundResult(bool Success, string? ProviderRefundId, string? FailureReason);
