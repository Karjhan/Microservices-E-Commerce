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

namespace Application.Features.Payments.RefundPayment;

public class RefundPaymentHandler(
    IPaymentRepository repo,
    IEventPublisher publisher,
    ICacheService cache,
    IPaymentProvider paymentProvider,
    ILogger<RefundPaymentHandler> logger)
    : IRequestHandler<RefundPaymentCommand>
{
    public async Task Handle(RefundPaymentCommand request, CancellationToken ct)
    {
        var payment = await repo.GetByIdAsync(request.PaymentId, ct)
            ?? throw new PaymentNotFoundException(request.PaymentId);

        var refundAmount = request.Amount ?? (payment.Amount - payment.RefundedAmount);

        payment.ApplyRefund(refundAmount);

        var result = await paymentProvider.RefundAsync(
            payment.ProviderPaymentId!,
            refundAmount,
            ct);

        if (!result.Success)
            throw new InvalidRefundException(result.FailureReason ?? "Stripe refund failed");

        var transaction = new PaymentTransaction(
            payment.Id,
            TransactionType.Refund,
            refundAmount,
            result.ProviderRefundId,
            success: true);

        await repo.AddTransactionAsync(transaction, ct);
        payment.AddTransaction(transaction);

        await repo.SaveChangesAsync(ct);

        logger.LogInformation(
            "Payment {PaymentId} refunded {Amount} {Currency}. Stripe refund: {RefundId}. Total refunded: {TotalRefunded}",
            payment.Id, refundAmount, payment.Currency, result.ProviderRefundId, payment.RefundedAmount);

        await cache.RemoveAsync($"payment:{payment.Id}");
        await cache.RemoveAsync($"payment:order:{payment.OrderId}");
        await cache.RemoveByPrefixAsync("payments:");

        await publisher.PublishAsync(new PaymentRefunded
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            UserId = payment.UserId,
            RefundAmount = refundAmount,
            TotalRefunded = payment.RefundedAmount,
            OriginalAmount = payment.Amount,
            Currency = payment.Currency,
            IsFullRefund = payment.Status == PaymentStatus.Refunded,
            RefundedAt = DateTime.UtcNow
        }, "payment.refunded");
    }
}
