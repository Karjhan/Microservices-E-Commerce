using System.Text.Json;
using Application.DTOs;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.GetProducts;

public class GetProductsHandler(IProductRepository repo, ICacheService cache)
    : IRequestHandler<GetProductsQuery, PaginatedListDto<ProductDto>>
{
    public async Task<PaginatedListDto<ProductDto>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var cacheKey = CacheKeyGenerator.ForProducts(request.Filter);

        var cached = await cache.GetAsync<PaginatedListDto<ProductDto>>(cacheKey);
        if (cached is not null)
            return cached;

        var products = await repo.GetFilteredAsync(request.Filter, ct);
        var totalCount = await repo.CountAsync(request.Filter, ct);

        var result = new PaginatedListDto<ProductDto>
        {
            Items = products.Select(ProductDto.FromEntity).ToList(),
            Page = request.Filter.Page,
            PageSize = request.Filter.PageSize,
            TotalCount = totalCount
        };

        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));

        return result;
    }
}