using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using MediatR;

namespace Application.Features.Orders.GetMyOrders;

public class GetMyOrdersHandler(IOrderRepository repo, ICacheService cache)
    : IRequestHandler<GetMyOrdersQuery, List<OrderDto>>
{
    public async Task<List<OrderDto>> Handle(GetMyOrdersQuery request, CancellationToken ct)
    {
        var cacheKey = $"orders:user:{request.UserId}";

        var cached = await cache.GetAsync<List<OrderDto>>(cacheKey);
        if (cached is not null)
            return cached;

        var orders = await repo.GetByUserIdAsync(request.UserId, ct);

        var result = orders
            .Select(OrderDto.FromEntity)
            .ToList();

        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2));

        return result;
    }
}
