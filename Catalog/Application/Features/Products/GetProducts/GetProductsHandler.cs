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
        // The shopper list only ever shows Active products; admins use the admin list (FR-016).
        var filter = request.Filter.OnlyActive();

        var cacheKey = CacheKeyGenerator.ForProducts(filter);

        var cached = await cache.GetAsync<PaginatedListDto<ProductDto>>(cacheKey);
        if (cached is not null)
            return cached;

        var products = await repo.GetFilteredAsync(filter, ct);
        var totalCount = await repo.CountAsync(filter, ct);

        var result = new PaginatedListDto<ProductDto>
        {
            Items = products.Select(ProductDto.FromEntity).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = totalCount
        };

        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));

        return result;
    }
}