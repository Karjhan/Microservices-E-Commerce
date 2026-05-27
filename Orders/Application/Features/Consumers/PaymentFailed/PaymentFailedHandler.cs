using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Contracts.Events;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Consumers.PaymentFailed;

public record PaymentFailedCommand(Guid OrderId, string Reason) : IRequest;

public class PaymentFailedHandler(
    IOrderRepository repo,
    IEventPublisher publisher,
    ICacheService cache)
    : IRequestHandler<PaymentFailedCommand>
{
    public async Task Handle(PaymentFailedCommand request, CancellationToken ct)
    {
        var order = await repo.GetByIdAsync(request.OrderId, ct)
            ?? throw new OrderNotFoundException(request.OrderId);

        order.MarkPaymentFailed();

        await repo.SaveChangesAsync(ct);

        await cache.RemoveAsync($"order:{order.Id}");
        await cache.RemoveByPrefixAsync("orders:");

        await publisher.PublishAsync(new OrderPaymentFailed
        {
            OrderId = order.Id,
            UserId = order.UserId,
            Reason = request.Reason,
            FailedAt = DateTime.UtcNow
        }, "order.payment_failed");
    }
}
