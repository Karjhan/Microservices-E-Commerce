using Application.Abstractions.Payments;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Infrastructure.Payments;

public class StripePaymentProvider : IPaymentProvider
{
    private readonly IStripeClient _client;
    private readonly ILogger<StripePaymentProvider> _logger;

    public StripePaymentProvider(IStripeClient client, ILogger<StripePaymentProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<ProviderChargeResult> ChargeAsync(
        string paymentToken,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var service = new PaymentIntentService(_client);

        var options = new PaymentIntentCreateOptions
        {
            Amount = ToStripeAmount(amount),
            Currency = currency.ToLowerInvariant(),
            PaymentMethod = paymentToken,
            Confirm = true,
            ReturnUrl = "https://example.com/payment/return"
        };

        var requestOptions = new RequestOptions
        {
            IdempotencyKey = idempotencyKey
        };

        try
        {
            var intent = await service.CreateAsync(options, requestOptions, ct);

            _logger.LogInformation(
                "Stripe PaymentIntent {IntentId} created with status: {Status}",
                intent.Id, intent.Status);

            return intent.Status switch
            {
                "succeeded" => new ProviderChargeResult(true, intent.Id, null),

                "requires_action" => new ProviderChargeResult(
                    false, null,
                    "3D Secure authentication required. Use the client SDK to complete confirmation."),

                _ => new ProviderChargeResult(
                    false, null,
                    $"Payment not completed. Stripe status: {intent.Status}")
            };
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex,
                "Stripe charge failed. Code: {Code}, DeclineCode: {DeclineCode}, Message: {Message}",
                ex.StripeError?.Code, ex.StripeError?.DeclineCode, ex.Message);

            return new ProviderChargeResult(false, null,
                ex.StripeError?.Message ?? ex.Message);
        }
    }

    public async Task<ProviderRefundResult> RefundAsync(
        string providerPaymentId,
        decimal amount,
        CancellationToken ct = default)
    {
        var service = new RefundService(_client);

        var options = new RefundCreateOptions
        {
            PaymentIntent = providerPaymentId,
            Amount = ToStripeAmount(amount)
        };

        try
        {
            var refund = await service.CreateAsync(options, cancellationToken: ct);

            _logger.LogInformation(
                "Stripe Refund {RefundId} created with status: {Status}",
                refund.Id, refund.Status);

            return refund.Status == "succeeded"
                ? new ProviderRefundResult(true, refund.Id, null)
                : new ProviderRefundResult(false, null, $"Refund not completed. Stripe status: {refund.Status}");
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex,
                "Stripe refund failed. Code: {Code}, Message: {Message}",
                ex.StripeError?.Code, ex.Message);

            return new ProviderRefundResult(false, null,
                ex.StripeError?.Message ?? ex.Message);
        }
    }

    private static long ToStripeAmount(decimal amount) => (long)Math.Round(amount * 100);
}
