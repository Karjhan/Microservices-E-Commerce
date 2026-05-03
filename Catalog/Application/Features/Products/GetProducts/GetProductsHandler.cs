using System.Text.Json;
using Application.DTOs;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.GetProducts;

public class GetProductsHandler(IProductRepository repo, ICacheService cache)
    : IRequestHandler<GetProductsQuery, List<ProductDto>>
{
    public async Task<List<ProductDto>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var cacheKey = CacheKeyGenerator.ForProducts(request.Filter);

        var cached = await cache.GetAsync<List<ProductDto>>(cacheKey);
        if (cached is not null)
            return cached;

        var products = await repo.GetFilteredAsync(request.Filter, ct);

        var result = products
            .Select(ProductDto.FromEntity)
            .ToList();

        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));

        return result;
    }
}