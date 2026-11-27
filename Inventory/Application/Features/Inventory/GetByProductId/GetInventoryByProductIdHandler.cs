using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using Domain.Exceptions;
using MediatR;
using Shared.Security.Authorization;

namespace Application.Features.Inventory.GetByProductId;

public class GetInventoryByProductIdHandler(
    IInventoryRepository repo,
    ICacheService cache,
    ICurrentUser currentUser)
    : IRequestHandler<GetInventoryByProductIdQuery, InventoryLookupResult>
{
    public async Task<InventoryLookupResult> Handle(GetInventoryByProductIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"inventory:product:{request.ProductId}";

        var dto = await cache.GetAsync<InventoryItemDto>(cacheKey);

        if (dto is null || string.IsNullOrEmpty(dto.Version))
        {
            var item = await repo.GetByProductIdAsync(request.ProductId, ct)
                ?? throw new InventoryItemNotFoundException(request.ProductId);

            dto = InventoryItemDto.FromEntity(item);
            await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));
        }

        var shopperView = PublicInventoryDto.FromDto(dto);

        return currentUser.IsAdmin
            ? new InventoryLookupResult(shopperView, dto)
            : new InventoryLookupResult(shopperView);
    }
}
