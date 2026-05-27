using Application.Abstractions.Persistence;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _context;

    public OrderRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Order order, CancellationToken ct)
    {
        await _context.Orders.AddAsync(order, ct);
    }

    public async Task AddItemAsync(OrderItem item, CancellationToken ct)
    {
        await _context.OrderItems.AddAsync(item, ct);
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.Orders
            .Include(x => x.Items)
            .Include(x => x.ShippingAddress)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<List<Order>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.ShippingAddress)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<Order>> GetFilteredAsync(OrderFilter filter, CancellationToken ct)
    {
        var query = _context.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.ShippingAddress)
            .AsQueryable();

        if (filter.UserId.HasValue)
            query = query.Where(x => x.UserId == filter.UserId.Value);

        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(x => x.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(x => x.CreatedAt <= filter.ToDate.Value);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }
}
