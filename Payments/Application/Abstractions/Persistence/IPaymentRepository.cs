using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Persistence;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken ct);
    Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken ct);
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct);
    Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);
    Task<List<Payment>> GetFilteredAsync(PaymentFilter filter, CancellationToken ct);
    Task<List<Payment>> GetExpiredPendingPaymentsAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public class PaymentFilter
{
    public Guid? UserId { get; init; }
    public Guid? OrderId { get; init; }
    public PaymentStatus? Status { get; init; }
    public PaymentMethod? Method { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
