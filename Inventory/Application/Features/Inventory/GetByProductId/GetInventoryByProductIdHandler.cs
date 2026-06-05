using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Inventory.GetByProductId;

public class GetInventoryByProductIdHandler(
    IInventoryRepository repo,
    ICacheService cache)
    : IRequestHandler<GetInventoryByProductIdQuery, InventoryItemDto>
{
    public async Task<InventoryItemDto> Handle(GetInventoryByProductIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"inventory:product:{request.ProductId}";

        var cached = await cache.GetAsync<InventoryItemDto>(cacheKey);
        if (cached is not null)
            return cached;

        var item = await repo.GetByProductIdAsync(request.ProductId, ct)
            ?? throw new InventoryItemNotFoundException(request.ProductId);

        var dto = InventoryItemDto.FromEntity(item);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));

        return dto;
    }
}
