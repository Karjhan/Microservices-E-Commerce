using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.Common;
using Domain.Exceptions;
using MediatR;
using Shared.Security.Authorization;

namespace Application.Features.Orders.UpdateShippingAddress;

public class UpdateShippingAddressHandler(
    IOrderRepository repo,
    ICacheService cache,
    ICurrentUser user)
    : IRequestHandler<UpdateShippingAddressCommand>
{
    public async Task Handle(UpdateShippingAddressCommand request, CancellationToken ct)
    {
        var order = await repo.GetByIdAsync(request.OrderId, ct)
            ?? throw new OrderNotFoundException(request.OrderId);

        OrderAccess.EnsureOwnerOrAdmin(user, order.UserId, order.Id);

        order.UpdateShippingAddress(
            request.Street,
            request.City,
            request.State,
            request.Country,
            request.ZipCode,
            request.FullName,
            request.Phone);

        await repo.SaveChangesAsync(ct);

        await cache.RemoveAsync($"order:{order.Id}");
        await cache.RemoveByPrefixAsync("orders:");
    }
}
