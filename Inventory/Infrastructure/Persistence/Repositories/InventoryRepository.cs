using Application.Abstractions.Persistence;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Infrastructure.Persistence.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly InventoryDbContext _context;

    public InventoryRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public Task<InventoryItem?> GetByProductIdAsync(Guid productId, CancellationToken ct)
    {
        return _context.InventoryItems
            .FirstOrDefaultAsync(x => x.ProductId == productId, ct);
    }

    public async Task<List<InventoryItem>> GetByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct)
    {
        if (productIds.Count == 0)
            return new List<InventoryItem>();

        return await _context.InventoryItems
            .Where(x => productIds.Contains(x.ProductId))
            .ToListAsync(ct);
    }

    public async Task<List<InventoryItem>> GetFilteredAsync(InventoryFilter filter, CancellationToken ct)
    {
        // The most urgent first: out of stock, then low, then the rest by how few units are available.
        return await ApplyFilter(_context.InventoryItems.AsNoTracking(), filter)
            .OrderBy(x => x.OnHandQuantity - x.ReservedQuantity <= 0
                ? 0
                : x.OnHandQuantity - x.ReservedQuantity <= x.LowStockThreshold ? 1 : 2)
            .ThenBy(x => x.OnHandQuantity - x.ReservedQuantity)
            .ThenBy(x => x.ProductId)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(InventoryFilter filter, CancellationToken ct)
    {
        return ApplyFilter(_context.InventoryItems.AsNoTracking(), filter).CountAsync(ct);
    }

    private static IQueryable<InventoryItem> ApplyFilter(IQueryable<InventoryItem> query, InventoryFilter filter)
    {
        if (filter.ProductIds is { Count: > 0 } ids)
        {
            query = query.Where(x => ids.Contains(x.ProductId));
        }

        return filter.Status switch
        {
            StockStatus.Out => query.Where(x => x.OnHandQuantity - x.ReservedQuantity <= 0),
            StockStatus.Low => query.Where(x =>
                x.OnHandQuantity - x.ReservedQuantity > 0
                && x.OnHandQuantity - x.ReservedQuantity <= x.LowStockThreshold),
            _ => query
        };
    }

    public async Task<List<InventoryMovement>> GetMovementsAsync(
        Guid productId,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        return await _context.InventoryMovements
            .AsNoTracking()
            .Where(x => x.ProductId == productId)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<int> CountMovementsAsync(Guid productId, CancellationToken ct)
    {
        return _context.InventoryMovements.CountAsync(x => x.ProductId == productId, ct);
    }
    public async Task<List<StockReservation>> GetReservationsByOrderIdAsync(Guid orderId, CancellationToken ct)
    {
        return await _context.StockReservations
            .Where(x => x.OrderId == orderId)
            .ToListAsync(ct);
    }

    public async Task<List<StockReservation>> GetActiveExpiredReservationsAsync(
        DateTime asOf,
        int batchSize,
        CancellationToken ct)
    {
        return await _context.StockReservations
            .Where(x => x.Status == ReservationStatus.Active && x.ExpiresAt <= asOf)
            .OrderBy(x => x.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task AddItemAsync(InventoryItem item, CancellationToken ct)
    {
        await _context.InventoryItems.AddAsync(item, ct);
    }

    public async Task AddReservationAsync(StockReservation reservation, CancellationToken ct)
    {
        await _context.StockReservations.AddAsync(reservation, ct);
    }

    public async Task AddMovementAsync(InventoryMovement movement, CancellationToken ct)
    {
        await _context.InventoryMovements.AddAsync(movement, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                $"The stock changed while this change was being saved: {ex.Message}");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two requests created the stock record of the same product at once.
            throw new ConcurrencyConflictException("The stock was created by another request. Reload and try again.");
        }
    }

    public void DiscardPendingChanges() => _context.ChangeTracker.Clear();

    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        return new EfTransactionScope(transaction);
    }

    private sealed class EfTransactionScope : ITransactionScope
    {
        private readonly IDbContextTransaction _transaction;

        public EfTransactionScope(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken ct) => _transaction.CommitAsync(ct);

        public Task RollbackAsync(CancellationToken ct) => _transaction.RollbackAsync(ct);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
