using Application.DTOs;
using Domain.Enums;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;
using Shared.Security.Authorization;

namespace Application.Features.Products.GetProductById;

public class GetProductByIdHandler(IProductRepository repo, ICacheService cache, ICurrentUser user)
    : IRequestHandler<GetProductByIdQuery, ProductDto>
{
    public async Task<ProductDto> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"product:{request.ProductId}";

        var cached = await cache.GetAsync<ProductDto>(cacheKey);
        if (cached is not null)
            return EnsureVisible(cached);

        var product = await repo.GetByIdAsync(request.ProductId, ct);

        if (product is null)
            throw new KeyNotFoundException("Product not found");

        var dto = ProductDto.FromEntity(product);

        // The cache holds the product whatever its status; visibility is decided per caller below.
        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));

        return EnsureVisible(dto);
    }

    /// <summary>Shoppers only see Active products; anything else looks like it does not exist (FR-016).</summary>
    private ProductDto EnsureVisible(ProductDto dto)
    {
        if (!user.IsAdmin && dto.Status != nameof(ProductStatus.Active))
            throw new KeyNotFoundException("Product not found");

        return dto;
    }
}
