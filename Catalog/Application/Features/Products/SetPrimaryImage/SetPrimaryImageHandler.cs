using Application.Common;
using Application.DTOs;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.SetPrimaryImage;

public class SetPrimaryImageHandler(IProductRepository repo, ProductCacheInvalidator cache)
    : IRequestHandler<SetPrimaryImageCommand, IReadOnlyList<ProductImageDto>>
{
    public async Task<IReadOnlyList<ProductImageDto>> Handle(SetPrimaryImageCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct)
                      ?? throw new KeyNotFoundException("Product not found");

        if (product.Images.All(x => x.Id != request.ImageId))
            throw new KeyNotFoundException("Image not found for this product");

        // Clear first and save, so the one-primary index never sees two primaries at once.
        product.ClearPrimaryImage();
        await repo.SaveChangesAsync(ct);

        product.SetPrimaryImage(request.ImageId);
        await repo.SaveChangesAsync(ct);

        await cache.InvalidateAsync(product.Id);

        return product.Images.OrderBy(x => x.SortOrder).Select(ProductImageDto.FromEntity).ToList();
    }
}
