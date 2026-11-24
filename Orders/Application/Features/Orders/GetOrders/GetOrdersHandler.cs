using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using MediatR;

namespace Application.Features.Orders.GetOrders;

public class GetOrdersHandler(IOrderRepository repo, ICacheService cache)
    : IRequestHandler<GetOrdersQuery, PaginatedListDto<OrderDto>>
{
    public async Task<PaginatedListDto<OrderDto>> Handle(GetOrdersQuery request, CancellationToken ct)
    {
        var cacheKey = $"orders:{ComputeHash(request.Filter)}";

        var cached = await cache.GetAsync<PaginatedListDto<OrderDto>>(cacheKey);
        if (cached is not null)
            return cached;

        var orders = await repo.GetFilteredAsync(request.Filter, ct);
        var totalCount = await repo.CountAsync(request.Filter, ct);

        var result = new PaginatedListDto<OrderDto>
        {
            Items = orders.Select(OrderDto.FromEntity).ToList(),
            Page = request.Filter.Page,
            PageSize = request.Filter.PageSize,
            TotalCount = totalCount
        };
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
