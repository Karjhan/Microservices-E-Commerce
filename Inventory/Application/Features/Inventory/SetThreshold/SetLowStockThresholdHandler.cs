using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Inventory.SetThreshold;

public class SetLowStockThresholdHandler(IInventoryRepository repo, ICacheService cache)
    : IRequestHandler<SetLowStockThresholdCommand, InventoryItemDto>
{
    public async Task<InventoryItemDto> Handle(SetLowStockThresholdCommand request, CancellationToken ct)
    {
        if (request.LowStockThreshold < 0 || request.LowStockThreshold > InventoryItem.MaxLowStockThreshold)
            throw new InventoryValidationException(
                "lowStockThreshold",
                $"Threshold must be a whole number from 0 to {InventoryItem.MaxLowStockThreshold}.");

        var item = await repo.GetByProductIdAsync(request.ProductId, ct)
            ?? throw new InventoryItemNotFoundException(request.ProductId);

        item.SetLowStockThreshold(request.LowStockThreshold);

        // The audit pipeline saves this together with the audit entry.
        await repo.SaveChangesAsync(ct);

        await cache.RemoveAsync($"inventory:product:{item.ProductId}");
        await cache.RemoveByPrefixAsync("inventory:list:");

        return InventoryItemDto.FromEntity(item);
    }
}
