using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.DTOs;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Inventory.Adjust;

public class AdjustInventoryHandler(
    IInventoryRepository repo,
    IEventPublisher publisher,
    ICacheService cache)
    : IRequestHandler<AdjustInventoryCommand, InventoryItemDto>
{
    public async Task<InventoryItemDto> Handle(AdjustInventoryCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3)
            throw new InvalidAdjustmentException("Adjustment reason is required (minimum 3 characters).");

        var item = await repo.GetByProductIdAsync(request.ProductId, ct);

        if (item is null)
        {
            if (request.Delta < 0)
                throw new InventoryItemNotFoundException(request.ProductId);

            item = new InventoryItem(request.ProductId, initialOnHand: request.Delta);
            await repo.AddItemAsync(item, ct);
        }
        else
        {
            item.Adjust(request.Delta);
        }

        var movement = new InventoryMovement(
            productId: item.ProductId,
            movementType: MovementType.Adjust,
            quantityDelta: request.Delta,
            onHandAfter: item.OnHandQuantity,
            reservedAfter: item.ReservedQuantity,
            sourceTrigger: "manual.adjust",
            reason: request.Reason);

        await repo.AddMovementAsync(movement, ct);
        await repo.SaveChangesAsync(ct);

        await cache.RemoveAsync($"inventory:product:{item.ProductId}");
        await cache.RemoveByPrefixAsync("inventory:list:");

        await publisher.PublishAsync(new InventoryUpdated
        {
            ProductId = item.ProductId,
            OnHandQuantity = item.OnHandQuantity,
            ReservedQuantity = item.ReservedQuantity,
            AvailableQuantity = item.AvailableQuantity,
            UpdatedAt = item.UpdatedAt
        }, "inventory.updated");

        if (item.IsDepleted)
        {
            await publisher.PublishAsync(new InventoryDepleted
            {
                ProductId = item.ProductId,
                DepletedAt = item.UpdatedAt
            }, "inventory.depleted");
        }

        return InventoryItemDto.FromEntity(item);
    }
}
