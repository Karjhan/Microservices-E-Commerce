using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Payments.GetPaymentById;

public class GetPaymentByIdHandler(IPaymentRepository repo, ICacheService cache)
    : IRequestHandler<GetPaymentByIdQuery, PaymentDto>
{
    public async Task<PaymentDto> Handle(GetPaymentByIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"payment:{request.PaymentId}";

        var cached = await cache.GetAsync<PaymentDto>(cacheKey);
        if (cached is not null)
            return cached;

        var payment = await repo.GetByIdAsync(request.PaymentId, ct)
            ?? throw new PaymentNotFoundException(request.PaymentId);

        var dto = PaymentDto.FromEntity(payment);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));

        return dto;
    }
}
