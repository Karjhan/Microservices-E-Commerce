using Application.Abstractions.Caching;
using Application.Abstractions.Inventory;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Inventory.ReserveStock;

public class ReserveStockHandler(
    IInventoryRepository repo,
    IEventPublisher publisher,
    ICacheService cache,
    IReservationPolicy reservationPolicy,
    ILogger<ReserveStockHandler> logger)
    : IRequestHandler<ReserveStockCommand, ReserveStockResult>
{
    private const int MaxConcurrencyRetries = 3;

    public async Task<ReserveStockResult> Handle(ReserveStockCommand request, CancellationToken ct)
    {
        if (request.Lines.Count == 0)
            return new ReserveStockResult(false, "no_lines", Array.Empty<Guid>());

        var existing = await repo.GetReservationsByOrderIdAsync(request.OrderId, ct);

        if (existing.Any(r => r.Status == ReservationStatus.Active
                              || r.Status == ReservationStatus.Deducted))
        {
            logger.LogInformation(
                "Reservation for order {OrderId} already exists. Treating as idempotent no-op.",
                request.OrderId);
            return new ReserveStockResult(true, "duplicate", Array.Empty<Guid>());
        }

        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            try
            {
                return await TryReserve(request, ct);
            }
            catch (ConcurrencyConflictException)
            {
                if (attempt == MaxConcurrencyRetries)
                {
                    logger.LogWarning(
                        "Reservation for order {OrderId} exhausted {Retries} concurrency retries.",
                        request.OrderId, MaxConcurrencyRetries);
                    throw;
                }

                logger.LogInformation(
                    "Concurrency conflict reserving order {OrderId} (attempt {Attempt}). Retrying.",
                    request.OrderId, attempt);
            }
        }

        return new ReserveStockResult(false, "concurrency_exhausted", Array.Empty<Guid>());
    }

    private async Task<ReserveStockResult> TryReserve(ReserveStockCommand request, CancellationToken ct)
    {
        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToArray();

        await using var tx = await repo.BeginTransactionAsync(ct);

        var items = await repo.GetByProductIdsAsync(productIds, ct);
        var itemsByProduct = items.ToDictionary(x => x.ProductId);

        var unavailable = new List<Guid>();
        foreach (var line in request.Lines)
        {
            if (!itemsByProduct.TryGetValue(line.ProductId, out var item))
            {
                unavailable.Add(line.ProductId);
                continue;
            }

            if (item.AvailableQuantity < line.Quantity)
            {
                unavailable.Add(line.ProductId);
            }
        }

        if (unavailable.Count > 0)
        {
            await tx.RollbackAsync(ct);

            foreach (var item in items)
            {
                await publisher.PublishAsync(new InventoryUpdated
                {
                    ProductId = item.ProductId,
                    OnHandQuantity = item.OnHandQuantity,
                    ReservedQuantity = item.ReservedQuantity,
                    AvailableQuantity = item.AvailableQuantity,
                    UpdatedAt = item.UpdatedAt
                }, "inventory.updated");
            }

            logger.LogInformation(
                "Reservation for order {OrderId} rejected: {Count} unavailable lines.",
                request.OrderId, unavailable.Count);

            return new ReserveStockResult(false, "insufficient_stock", unavailable);
        }

        var expiresAt = DateTime.UtcNow.Add(reservationPolicy.ReservationTimeout);

        foreach (var line in request.Lines)
        {
            var item = itemsByProduct[line.ProductId];

            item.Reserve(line.Quantity);

            var reservation = new StockReservation(
                orderId: request.OrderId,
                productId: line.ProductId,
                quantity: line.Quantity,
                expiresAt: expiresAt);

            await repo.AddReservationAsync(reservation, ct);

            var movement = new InventoryMovement(
                productId: item.ProductId,
                movementType: MovementType.Reserve,
                quantityDelta: line.Quantity,
                onHandAfter: item.OnHandQuantity,
                reservedAfter: item.ReservedQuantity,
                sourceTrigger: "order.created",
                orderId: request.OrderId,
                reservationId: reservation.Id,
                correlationId: request.CorrelationId);

            await repo.AddMovementAsync(movement, ct);
        }

        await repo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await publisher.PublishAsync(new InventoryReserved
        {
            OrderId = request.OrderId,
            ReservedAt = DateTime.UtcNow,
            Lines = request.Lines
                .Select(l => new InventoryReservedLine(l.ProductId, l.Quantity))
                .ToList()
        }, "inventory.reserved");

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
            "Reserved stock for order {OrderId} across {LineCount} lines.",
            request.OrderId, request.Lines.Count);

        return new ReserveStockResult(true, null, Array.Empty<Guid>());
    }
}
