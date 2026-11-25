using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.Common;
using Application.DTOs;
using MediatR;
using Shared.Security.Authorization;

namespace Application.Features.Payments.GetPaymentByOrderId;

public class GetPaymentByOrderIdHandler(IPaymentRepository repo, ICacheService cache, ICurrentUser user)
    : IRequestHandler<GetPaymentByOrderIdQuery, PaymentDto>
{
    public async Task<PaymentDto> Handle(GetPaymentByOrderIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"payment:order:{request.OrderId}";

        var cached = await cache.GetAsync<PaymentDto>(cacheKey);
        if (cached is not null)
        {
            // The cache is shared by every caller, so ownership is checked on a hit too.
            PaymentAccess.EnsureOwnerOrAdmin(user, cached.UserId, cached.Id);
            return cached;
        }

        var payment = await repo.GetByOrderIdAsync(request.OrderId, ct);

        if (payment is null)
            throw new KeyNotFoundException($"No payment found for order {request.OrderId}");

        PaymentAccess.EnsureOwnerOrAdmin(user, payment.UserId, payment.Id);

        var dto = PaymentDto.FromEntity(payment);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));

        return dto;
    }
}
