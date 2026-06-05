using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Inventory.DeductStock;

public class DeductStockHandler(
    IInventoryRepository repo,
    IEventPublisher publisher,
    ICacheService cache,
    ILogger<DeductStockHandler> logger)
    : IRequestHandler<DeductStockCommand, DeductStockResult>
{
    public async Task<DeductStockResult> Handle(DeductStockCommand request, CancellationToken ct)
    {
        var reservations = await repo.GetReservationsByOrderIdAsync(request.OrderId, ct);

        if (reservations.Count == 0)
        {
            logger.LogWarning(
                "Payment completed for order {OrderId} but no reservation found; recoverable exception.",
                request.OrderId);

            return new DeductStockResult(false, "no_reservation");
        }

        var activeReservations = reservations
            .Where(r => r.Status == ReservationStatus.Active)
            .ToList();

        if (activeReservations.Count == 0)
        {
            if (reservations.All(r => r.Status == ReservationStatus.Deducted))
            {
                logger.LogInformation(
                    "Deduction for order {OrderId} already applied (all reservations Deducted); idempotent no-op.",
                    request.OrderId);
                return new DeductStockResult(true, "already_deducted");
            }

            logger.LogWarning(
                "Reservations for order {OrderId} are terminal but not Deducted ({Statuses}); skipping.",
                request.OrderId,
                string.Join(",", reservations.Select(r => r.Status)));

            return new DeductStockResult(false, "terminal_not_active");
        }

        var productIds = activeReservations.Select(r => r.ProductId).Distinct().ToArray();

        await using var tx = await repo.BeginTransactionAsync(ct);

        var items = await repo.GetByProductIdsAsync(productIds, ct);
        var itemsByProduct = items.ToDictionary(x => x.ProductId);

        foreach (var reservation in activeReservations)
        {
            var item = itemsByProduct[reservation.ProductId];

            item.Deduct(reservation.Quantity);
            reservation.Deduct();

            var movement = new InventoryMovement(
                productId: item.ProductId,
                movementType: MovementType.Deduct,
                quantityDelta: -reservation.Quantity,
                onHandAfter: item.OnHandQuantity,
                reservedAfter: item.ReservedQuantity,
                sourceTrigger: "payment.completed",
                orderId: request.OrderId,
                reservationId: reservation.Id,
                correlationId: request.CorrelationId);

            await repo.AddMovementAsync(movement, ct);
        }

        await repo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

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

            if (item.IsDepleted)
            {
                await publisher.PublishAsync(new InventoryDepleted
                {
                    ProductId = item.ProductId,
                    DepletedAt = item.UpdatedAt
                }, "inventory.depleted");
            }
        }

        await cache.RemoveByPrefixAsync("inventory:list:");

        logger.LogInformation(
            "Deducted stock for order {OrderId} across {Count} reservations.",
            request.OrderId, activeReservations.Count);

        return new DeductStockResult(true, null);
    }
}
