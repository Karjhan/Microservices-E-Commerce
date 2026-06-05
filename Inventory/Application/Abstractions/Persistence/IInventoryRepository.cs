using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Persistence;

public interface IInventoryRepository
{
    Task<InventoryItem?> GetByProductIdAsync(Guid productId, CancellationToken ct);
    Task<List<InventoryItem>> GetByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct);
    Task<List<InventoryItem>> GetFilteredAsync(InventoryFilter filter, CancellationToken ct);
    Task<int> CountAsync(InventoryFilter filter, CancellationToken ct);

    Task<List<StockReservation>> GetReservationsByOrderIdAsync(Guid orderId, CancellationToken ct);
    Task<List<StockReservation>> GetActiveExpiredReservationsAsync(DateTime asOf, int batchSize, CancellationToken ct);

    Task AddItemAsync(InventoryItem item, CancellationToken ct);
    Task AddReservationAsync(StockReservation reservation, CancellationToken ct);
    Task AddMovementAsync(InventoryMovement movement, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);

    Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct);
}

public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);
}

public class InventoryFilter
{
    public bool? DepletedOnly { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
