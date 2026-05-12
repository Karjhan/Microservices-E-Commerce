using Application.Abstractions.Persistence;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentsDbContext _context;

    public PaymentRepository(PaymentsDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Payment payment, CancellationToken ct)
    {
        await _context.Payments.AddAsync(payment, ct);
    }

    public async Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken ct)
    {
        await _context.PaymentTransactions.AddAsync(transaction, ct);
    }

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.Payments
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct)
    {
        return await _context.Payments
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
    }

    public async Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        return await _context.Payments
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task<List<Payment>> GetFilteredAsync(PaymentFilter filter, CancellationToken ct)
    {
        var query = _context.Payments
            .AsNoTracking()
            .Include(x => x.Transactions)
            .AsQueryable();

        if (filter.UserId.HasValue)
            query = query.Where(x => x.UserId == filter.UserId.Value);

        if (filter.OrderId.HasValue)
            query = query.Where(x => x.OrderId == filter.OrderId.Value);

        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status.Value);

        if (filter.Method.HasValue)
            query = query.Where(x => x.Method == filter.Method.Value);

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

    public async Task<List<Payment>> GetExpiredPendingPaymentsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        return await _context.Payments
            .Where(x => x.Status == PaymentStatus.Pending && x.ExpiresAt <= now)
            .ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }
}
