using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using MediatR;

namespace Application.Features.Inventory.List;

public class ListInventoryHandler(
    IInventoryRepository repo,
    ICacheService cache)
    : IRequestHandler<ListInventoryQuery, InventoryListDto>
{
    public async Task<InventoryListDto> Handle(ListInventoryQuery request, CancellationToken ct)
    {
        var cacheKey = $"inventory:list:{ComputeHash(request.Filter)}";

        var cached = await cache.GetAsync<InventoryListDto>(cacheKey);
        if (cached is not null)
            return cached;

        var items = await repo.GetFilteredAsync(request.Filter, ct);
        var total = await repo.CountAsync(request.Filter, ct);

        var result = new InventoryListDto
        {
            Items = items.Select(InventoryItemDto.FromEntity).ToList(),
            Page = request.Filter.Page,
            PageSize = request.Filter.PageSize,
            TotalCount = total
        };

        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(1));

        return result;
    }

    private static string ComputeHash(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
