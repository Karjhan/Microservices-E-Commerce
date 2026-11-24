using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using MediatR;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application.Features.Payments.GetPayments;

public class GetPaymentsHandler(IPaymentRepository repo, ICacheService cache)
    : IRequestHandler<GetPaymentsQuery, PaginatedListDto<PaymentDto>>
{
    public async Task<PaginatedListDto<PaymentDto>> Handle(GetPaymentsQuery request, CancellationToken ct)
    {
        var cacheKey = $"payments:{ComputeHash(request.Filter)}";

        var cached = await cache.GetAsync<PaginatedListDto<PaymentDto>>(cacheKey);
        if (cached is not null)
            return cached;

        var payments = await repo.GetFilteredAsync(request.Filter, ct);
        var totalCount = await repo.CountAsync(request.Filter, ct);

        var result = new PaginatedListDto<PaymentDto>
        {
            Items = payments.Select(PaymentDto.FromEntity).ToList(),
            Page = request.Filter.Page,
            PageSize = request.Filter.PageSize,
            TotalCount = totalCount
        };

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
