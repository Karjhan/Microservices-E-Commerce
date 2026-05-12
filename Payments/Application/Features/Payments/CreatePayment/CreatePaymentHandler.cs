using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Contracts.Events;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Payments.CreatePayment;

public class CreatePaymentHandler(
    IPaymentRepository repo,
    IEventPublisher publisher,
    ICacheService cache)
    : IRequestHandler<CreatePaymentCommand, Guid>
{
    public async Task<Guid> Handle(CreatePaymentCommand request, CancellationToken ct)
    {
        var existing = await repo.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);

        if (existing is not null)
            throw new DuplicatePaymentException(request.IdempotencyKey);

        var payment = new Payment(
            request.OrderId,
            request.UserId,
            request.Amount,
            request.Currency,
            request.Method,
            request.IdempotencyKey,
            request.PaymentToken);

        await repo.AddAsync(payment, ct);
        await repo.SaveChangesAsync(ct);

        await cache.RemoveByPrefixAsync("payments:");

        await publisher.PublishAsync(new PaymentCreated
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            UserId = payment.UserId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Method = payment.Method.ToString(),
            Status = payment.Status.ToString(),
            IdempotencyKey = payment.IdempotencyKey,
            CreatedAt = payment.CreatedAt,
            ExpiresAt = payment.ExpiresAt
        }, "payment.created");

        return payment.Id;
    }
}
