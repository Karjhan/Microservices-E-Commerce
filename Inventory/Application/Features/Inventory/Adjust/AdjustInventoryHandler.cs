using Application.Abstractions.Caching;
using Application.Abstractions.Inventory;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.DTOs;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;
using Shared.Security.Authorization;

namespace Application.Features.Inventory.Adjust;

public class AdjustInventoryHandler(
    IInventoryRepository repo,
    IEventPublisher publisher,
    ICacheService cache,
    IStockSettings settings,
    ICurrentUser currentUser)
    : IRequestHandler<AdjustInventoryCommand, InventoryItemDto>
{
    public const int ReasonMinLength = 3;
    public const int ReasonMaxLength = 200;

    public async Task<InventoryItemDto> Handle(AdjustInventoryCommand request, CancellationToken ct)
    {
        var reason = (request.Reason ?? string.Empty).Trim();

        if (reason.Length < ReasonMinLength || reason.Length > ReasonMaxLength)
            throw new InventoryValidationException(
                "reason", $"Reason must be {ReasonMinLength} to {ReasonMaxLength} characters.");

        if (request.Mode == AdjustMode.Delta && request.Quantity == 0)
            throw new InventoryValidationException("quantity", "Enter a number other than zero.");

        if (Math.Abs((long)request.Quantity) > InventoryItem.MaxQuantity)
            throw new InventoryValidationException(
                "quantity", $"Quantity can be at most {InventoryItem.MaxQuantity}.");

        var item = await repo.GetByProductIdAsync(request.ProductId, ct);
        int delta;

        if (item is null)
        {
            // No record yet: the first change creates it. Removing units from nothing is not possible.
            if (request.Mode == AdjustMode.Delta && request.Quantity < 0)
                throw new InventoryItemNotFoundException(request.ProductId);

            item = new InventoryItem(request.ProductId, request.Quantity, settings.DefaultLowStockThreshold);
            delta = request.Quantity;
            await repo.AddItemAsync(item, ct);
        }
        else
        {
            if (request.ExpectedVersion is not null
                && !string.Equals(request.ExpectedVersion, Convert.ToBase64String(item.RowVersion), StringComparison.Ordinal))
            {
                throw new ConcurrencyConflictException(
                    "The stock changed since you opened it. Reload the product and try again.");
            }

            if (request.Mode == AdjustMode.Set)
            {
                delta = item.SetOnHand(request.Quantity);
            }
            else
            {
                item.Adjust(request.Quantity);
                delta = request.Quantity;
            }
        }

        var movement = new InventoryMovement(
            productId: item.ProductId,
            movementType: MovementType.Adjust,
            quantityDelta: delta,
            onHandAfter: item.OnHandQuantity,
            reservedAfter: item.ReservedQuantity,
            sourceTrigger: request.Mode == AdjustMode.Set ? "manual.set" : "manual.adjust",
            reason: reason,
            correlationId: currentUser.CorrelationId,
            actorId: currentUser.Id,
            actorEmail: currentUser.Email);

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
