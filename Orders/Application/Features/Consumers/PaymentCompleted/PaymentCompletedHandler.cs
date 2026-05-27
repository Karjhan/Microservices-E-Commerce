using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Contracts.Events;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Consumers.PaymentCompleted;

public record PaymentCompletedCommand(Guid OrderId, DateTime PaidAt) : IRequest;

public class PaymentCompletedHandler(
    IOrderRepository repo,
    IEventPublisher publisher,
    ICacheService cache)
    : IRequestHandler<PaymentCompletedCommand>
{
    public async Task Handle(PaymentCompletedCommand request, CancellationToken ct)
    {
        var order = await repo.GetByIdAsync(request.OrderId, ct)
            ?? throw new OrderNotFoundException(request.OrderId);

        order.MarkPaid();

        await repo.SaveChangesAsync(ct);

        await cache.RemoveAsync($"order:{order.Id}");
        await cache.RemoveByPrefixAsync("orders:");

        await publisher.PublishAsync(new OrderPaid
        {
            OrderId = order.Id,
            UserId = order.UserId,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            PaidAt = order.PaidAt!.Value
        }, "order.paid");
    }
}
