using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Payments.ProcessPayment;

public class ProcessPaymentHandler(
    IPaymentRepository repo,
    IEventPublisher publisher,
    ICacheService cache,
    IPaymentProvider paymentProvider,
    ILogger<ProcessPaymentHandler> logger)
    : IRequestHandler<ProcessPaymentCommand>
{
    public async Task Handle(ProcessPaymentCommand request, CancellationToken ct)
    {
        var payment = await repo.GetByIdAsync(request.PaymentId, ct)
            ?? throw new PaymentNotFoundException(request.PaymentId);

        if (payment.IsExpired)
        {
            payment.MarkCancelled();
            await repo.SaveChangesAsync(ct);

            await publisher.PublishAsync(new PaymentCancelled
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                UserId = payment.UserId,
                Reason = "Payment expired before processing",
                CancelledAt = DateTime.UtcNow
            }, "payment.cancelled");

            throw new InvalidPaymentStateException("Payment has expired and was cancelled.");
        }

        if (string.IsNullOrEmpty(payment.PaymentToken))
            throw new InvalidPaymentStateException(
                "Payment has no payment token. Cannot charge without a Stripe payment method.");

        payment.MarkProcessing();

        var result = await paymentProvider.ChargeAsync(
            payment.PaymentToken,
            payment.Amount,
            payment.Currency,
            payment.IdempotencyKey,
            ct);

        var transaction = new PaymentTransaction(
            payment.Id,
            TransactionType.Charge,
            payment.Amount,
            result.ProviderPaymentId,
            result.Success,
            result.FailureReason);

        await repo.AddTransactionAsync(transaction, ct);
        payment.AddTransaction(transaction);

        if (result.Success)
        {
            payment.MarkCompleted(result.ProviderPaymentId!);

            logger.LogInformation(
                "Payment {PaymentId} completed. Stripe PI: {StripeId}",
                payment.Id, result.ProviderPaymentId);

            await repo.SaveChangesAsync(ct);

            await cache.RemoveAsync($"payment:{payment.Id}");
            await cache.RemoveAsync($"payment:order:{payment.OrderId}");
            await cache.RemoveByPrefixAsync("payments:");

            await publisher.PublishAsync(new PaymentCompleted
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                UserId = payment.UserId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                ProviderPaymentId = result.ProviderPaymentId!,
                CompletedAt = payment.CompletedAt!.Value
            }, "payment.completed");
        }
        else
        {
            payment.MarkFailed(result.FailureReason!);

            logger.LogWarning(
                "Payment {PaymentId} failed. Reason: {Reason}",
                payment.Id, result.FailureReason);

            await repo.SaveChangesAsync(ct);

            await cache.RemoveAsync($"payment:{payment.Id}");
            await cache.RemoveByPrefixAsync("payments:");

            await publisher.PublishAsync(new PaymentFailed
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                UserId = payment.UserId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Reason = result.FailureReason!,
                FailedAt = DateTime.UtcNow
            }, "payment.failed");
        }
    }
}
