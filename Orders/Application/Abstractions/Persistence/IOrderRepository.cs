using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Persistence;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken ct);
    Task AddItemAsync(OrderItem item, CancellationToken ct);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<Order>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<List<Order>> GetFilteredAsync(OrderFilter filter, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public class OrderFilter
{
    public Guid? UserId { get; init; }
    public OrderStatus? Status { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
