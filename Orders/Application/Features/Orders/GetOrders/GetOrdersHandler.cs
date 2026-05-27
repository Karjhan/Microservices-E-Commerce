using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using MediatR;

namespace Application.Features.Orders.GetOrders;

public class GetOrdersHandler(IOrderRepository repo, ICacheService cache)
    : IRequestHandler<GetOrdersQuery, List<OrderDto>>
{
    public async Task<List<OrderDto>> Handle(GetOrdersQuery request, CancellationToken ct)
    {
        var cacheKey = $"orders:{ComputeHash(request.Filter)}";

        var cached = await cache.GetAsync<List<OrderDto>>(cacheKey);
        if (cached is not null)
            return cached;

        var orders = await repo.GetFilteredAsync(request.Filter, ct);

        var result = orders
            .Select(OrderDto.FromEntity)
            .ToList();

        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2));

        return result;
    }

    private static string ComputeHash(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
