using Application.Abstractions.Storage;
using Application.DTOs;
using Domain.Enums;
using Infrastructure.Abstractions.Persistence;
using MediatR;
using Shared.Security.Authorization;

namespace Application.Features.Products.GetModel;

public class GetProductModelHandler(
    IProductRepository repo,
    IFileStorageService storage,
    ICurrentUser user)
    : IRequestHandler<GetProductModelQuery, ProductModelDto>
{
    // Long enough to load a large model, short enough that a copied link expires quickly (research R8).
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

    public async Task<ProductModelDto> Handle(GetProductModelQuery request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct);

        // Shoppers only see models of active products; everything else looks like it does not exist.
        if (product is null || (!user.IsAdmin && product.Status != ProductStatus.Active))
            throw new KeyNotFoundException("Product not found");

        var model = product.Model ?? throw new KeyNotFoundException("This product has no 3D model");

        var url = await storage.GetPresignedUrlAsync(StorageBucket.Models, model.ObjectKey, LinkLifetime, ct);

        return ProductModelDto.FromEntity(model, url);
    }
}
