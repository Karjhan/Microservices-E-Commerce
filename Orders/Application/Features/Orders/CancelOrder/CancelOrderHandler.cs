using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Common;
using Contracts.Events;
using Domain.Exceptions;
using MediatR;
using Shared.Security.Authorization;

namespace Application.Features.Orders.CancelOrder;

public class CancelOrderHandler(
    IOrderRepository repo,
    IEventPublisher publisher,
    ICacheService cache,
    ICurrentUser user)
    : IRequestHandler<CancelOrderCommand>
{
    public async Task Handle(CancelOrderCommand request, CancellationToken ct)
    {
        var order = await repo.GetByIdAsync(request.OrderId, ct)
            ?? throw new OrderNotFoundException(request.OrderId);

        OrderAccess.EnsureOwnerOrAdmin(user, order.UserId, order.Id);

        order.MarkCancelled(request.Reason);

        await repo.SaveChangesAsync(ct);

        await cache.RemoveAsync($"order:{order.Id}");
        await cache.RemoveByPrefixAsync("orders:");

        await publisher.PublishAsync(new OrderCancelled
        {
            OrderId = order.Id,
            UserId = order.UserId,
            Reason = request.Reason,
            CancelledAt = order.CancelledAt!.Value,
            RecipientEmail = order.RecipientEmail,
        }, "order.cancelled");
    }
}
