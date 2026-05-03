using Application.DTOs;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.GetProductById;

public class GetProductByIdHandler(IProductRepository repo, ICacheService cache)
    : IRequestHandler<GetProductByIdQuery, ProductDto>
{
    public async Task<ProductDto> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"product:{request.ProductId}";

        var cached = await cache.GetAsync<ProductDto>(cacheKey);
        if (cached is not null)
            return cached;

        var product = await repo.GetByIdAsync(request.ProductId, ct);

        if (product is null)
            throw new KeyNotFoundException("Product not found");

        var dto = ProductDto.FromEntity(product);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));

        return dto;
    }
}