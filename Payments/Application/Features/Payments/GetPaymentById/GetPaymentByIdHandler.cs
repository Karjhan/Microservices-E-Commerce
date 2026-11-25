using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.Common;
using Application.DTOs;
using Domain.Exceptions;
using MediatR;
using Shared.Security.Authorization;

namespace Application.Features.Payments.GetPaymentById;

public class GetPaymentByIdHandler(IPaymentRepository repo, ICacheService cache, ICurrentUser user)
    : IRequestHandler<GetPaymentByIdQuery, PaymentDto>
{
    public async Task<PaymentDto> Handle(GetPaymentByIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"payment:{request.PaymentId}";

        var cached = await cache.GetAsync<PaymentDto>(cacheKey);
        if (cached is not null)
        {
            // The cache is shared by every caller, so ownership is checked on a hit too.
            PaymentAccess.EnsureOwnerOrAdmin(user, cached.UserId, request.PaymentId);
            return cached;
        }

        var payment = await repo.GetByIdAsync(request.PaymentId, ct)
            ?? throw new PaymentNotFoundException(request.PaymentId);

        PaymentAccess.EnsureOwnerOrAdmin(user, payment.UserId, payment.Id);

        var dto = PaymentDto.FromEntity(payment);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));

        return dto;
    }
}
