using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using MediatR;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application.Features.Payments.GetPayments;

public class GetPaymentsHandler(IPaymentRepository repo, ICacheService cache)
    : IRequestHandler<GetPaymentsQuery, List<PaymentDto>>
{
    public async Task<List<PaymentDto>> Handle(GetPaymentsQuery request, CancellationToken ct)
    {
        var cacheKey = $"payments:{ComputeHash(request.Filter)}";

        var cached = await cache.GetAsync<List<PaymentDto>>(cacheKey);
        if (cached is not null)
            return cached;

        var payments = await repo.GetFilteredAsync(request.Filter, ct);

        var result = payments
            .Select(PaymentDto.FromEntity)
            .ToList();

        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2));

        return result;
    }

    private static string ComputeHash(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
