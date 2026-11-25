using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.Common;
using Application.DTOs;
using Domain.Exceptions;
using MediatR;
using Shared.Security.Authorization;

namespace Application.Features.Orders.GetOrderById;

public class GetOrderByIdHandler(IOrderRepository repo, ICacheService cache, ICurrentUser user)
    : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"order:{request.OrderId}";

        var cached = await cache.GetAsync<OrderDto>(cacheKey);
        if (cached is not null)
        {
            // The cache is shared by every caller, so ownership is checked on a hit too.
            OrderAccess.EnsureOwnerOrAdmin(user, cached.UserId, request.OrderId);
            return cached;
        }

        var order = await repo.GetByIdAsync(request.OrderId, ct)
            ?? throw new OrderNotFoundException(request.OrderId);

        OrderAccess.EnsureOwnerOrAdmin(user, order.UserId, order.Id);

        var dto = OrderDto.FromEntity(order);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));

        return dto;
    }
}
