using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Inventory.ReleaseStock;

public class ReleaseStockHandler(
    IInventoryRepository repo,
    IEventPublisher publisher,
    ICacheService cache,
    ILogger<ReleaseStockHandler> logger)
    : IRequestHandler<ReleaseStockCommand, ReleaseStockResult>
{
    public async Task<ReleaseStockResult> Handle(ReleaseStockCommand request, CancellationToken ct)
    {
        var reservations = await repo.GetReservationsByOrderIdAsync(request.OrderId, ct);

        var activeReservations = reservations
            .Where(r => r.Status == ReservationStatus.Active)
            .ToList();

        if (activeReservations.Count == 0)
        {
            logger.LogInformation(
                "No active reservations to release for order {OrderId}; idempotent no-op (reason={Reason}).",
                request.OrderId, request.Reason);

            return new ReleaseStockResult(true, 0, "no_active_reservations");
        }

        var productIds = activeReservations.Select(r => r.ProductId).Distinct().ToArray();

        await using var tx = await repo.BeginTransactionAsync(ct);

        var items = await repo.GetByProductIdsAsync(productIds, ct);
        var itemsByProduct = items.ToDictionary(x => x.ProductId);

        foreach (var reservation in activeReservations)
        {
            if (!itemsByProduct.TryGetValue(reservation.ProductId, out var item))
            {
                logger.LogWarning(
                    "Reservation {ReservationId} points to missing inventory item {ProductId}; releasing reservation only.",
                    reservation.Id, reservation.ProductId);

                if (request.MarkExpired)
                    reservation.Expire();
                else
                    reservation.Release();
                continue;
            }

            item.Release(reservation.Quantity);

            if (request.MarkExpired)
                reservation.Expire();
            else
                reservation.Release();

            var movement = new InventoryMovement(
                productId: item.ProductId,
                movementType: MovementType.Release,
                quantityDelta: -reservation.Quantity,
                onHandAfter: item.OnHandQuantity,
                reservedAfter: item.ReservedQuantity,
                sourceTrigger: request.Reason,
                orderId: request.OrderId,
                reservationId: reservation.Id,
                correlationId: request.CorrelationId);

            await repo.AddMovementAsync(movement, ct);
        }

        await repo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await publisher.PublishAsync(new InventoryReleased
        {
            OrderId = request.OrderId,
            Reason = request.Reason,
            ReleasedAt = DateTime.UtcNow,
            Lines = activeReservations
                .Select(r => new InventoryReleasedLine(r.ProductId, r.Quantity))
                .ToList()
        }, "inventory.released");

        foreach (var item in itemsByProduct.Values)
        {
            await cache.RemoveAsync($"inventory:product:{item.ProductId}");

            await publisher.PublishAsync(new InventoryUpdated
            {
                ProductId = item.ProductId,
                OnHandQuantity = item.OnHandQuantity,
                ReservedQuantity = item.ReservedQuantity,
                AvailableQuantity = item.AvailableQuantity,
                UpdatedAt = item.UpdatedAt
            }, "inventory.updated");
        }

        await cache.RemoveByPrefixAsync("inventory:list:");

        logger.LogInformation(
            "Released {Count} reservations for order {OrderId} (reason={Reason}).",
            activeReservations.Count, request.OrderId, request.Reason);

        return new ReleaseStockResult(true, activeReservations.Count, null);
    }
}
