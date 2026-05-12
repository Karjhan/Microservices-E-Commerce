using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using MediatR;

namespace Application.Features.Payments.GetPaymentByOrderId;

public class GetPaymentByOrderIdHandler(IPaymentRepository repo, ICacheService cache)
    : IRequestHandler<GetPaymentByOrderIdQuery, PaymentDto>
{
    public async Task<PaymentDto> Handle(GetPaymentByOrderIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"payment:order:{request.OrderId}";

        var cached = await cache.GetAsync<PaymentDto>(cacheKey);
        if (cached is not null)
            return cached;

        var payment = await repo.GetByOrderIdAsync(request.OrderId, ct);

        if (payment is null)
            throw new KeyNotFoundException($"No payment found for order {request.OrderId}");

        var dto = PaymentDto.FromEntity(payment);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));

        return dto;
    }
}
