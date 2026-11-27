using Application.Common;
using Application.DTOs;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.ReorderImages;

public class ReorderImagesHandler(IProductRepository repo, ProductCacheInvalidator cache)
    : IRequestHandler<ReorderImagesCommand, IReadOnlyList<ProductImageDto>>
{
    public async Task<IReadOnlyList<ProductImageDto>> Handle(ReorderImagesCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct)
                      ?? throw new KeyNotFoundException("Product not found");

        product.ReorderImages(request.ImageIds);

        await repo.SaveChangesAsync(ct);
        await cache.InvalidateAsync(product.Id);

        return product.Images.OrderBy(x => x.SortOrder).Select(ProductImageDto.FromEntity).ToList();
    }
}
