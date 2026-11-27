using Application.Abstractions.Messaging;
using Application.Common;
using Contracts.Events;
using Domain.Entities;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.UpdateProduct;

public class UpdateProductHandler(
    IProductRepository repo,
    ICategoryRepository categories,
    IBrandRepository brands,
    IFilamentTypeRepository filamentTypes,
    IEventPublisher publisher,
    ICacheService cache)
    : IRequestHandler<UpdateProductCommand>
{
    public async Task Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct);

        if (product is null)
            throw new KeyNotFoundException("Product not found");

        await ProductRules.ValidateAsync(repo, product.Id, request.Name, request.Price, request.Currency, ct);

        // Entries the product already has stay valid even when deactivated; new ones must be active (US3 scenario 3).
        if (request.CategoryId.HasValue)
            await ProductReferenceRules.EnsureCategoryAsync(categories, request.CategoryId.Value, product.CategoryId, ct);

        await ProductReferenceRules.EnsureBrandAsync(brands, request.BrandId, product.BrandId, ct);

        var currentFilamentTypeIds = product.FilamentTypes.Select(x => x.FilamentTypeId).ToList();

        var filamentTypeIds = request.FilamentTypeIds is null
            ? null
            : await ProductReferenceRules.EnsureFilamentTypesAsync(
                filamentTypes, request.FilamentTypeIds, currentFilamentTypeIds, ct);

        product.UpdateDetails(
            request.Name.Trim(),
            request.ShortDescription,
            request.LongDescription,
            request.Price
        );

        // Download count and rating are never touched here: they are not admin-editable (FR-012).

        product.SetCurrency(ProductRules.NormalizeCurrency(request.Currency));
        product.SetSettings(request.Settings);
        product.SetSize(request.Size);

        if (request.Tags is not null)
            product.SetTags(request.Tags);

        if (request.CategoryId.HasValue)
            product.SetCategory(request.CategoryId.Value);

        product.SetBrand(request.BrandId);

        if (filamentTypeIds is not null)
            product.SetFilamentTypes(filamentTypeIds);

        if (request.CompatiblePrinters is not null)
            product.SetCompatiblePrinters(request.CompatiblePrinters);

        await repo.SaveChangesAsync(ct);

        await cache.RemoveAsync($"product:{product.Id}");
        await cache.RemoveByPrefixAsync("products:");

        await publisher.PublishAsync(new ProductUpdated
        {
            ProductId = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            ShortDescription = product.ShortDescription,
            LongDescription = product.LongDescription,
            Price = product.Price,
            Currency = product.Currency,
            Status = product.Status.ToString(),
            Tags = product.Tags.ToList(),
            DownloadCount =  product.DownloadCount,
            AverageRating = (int)product.AverageRating,
            SupportedMaterials = await GetFilamentCodesAsync(product, ct),
            CompatiblePrinters = product.CompatiblePrinters.Select(x => x.ToString()).ToList(),
            UpdatedAt = product.UpdatedAt ?? DateTime.UtcNow
        }, "product.updated");
    }

    private async Task<List<string>> GetFilamentCodesAsync(Product product, CancellationToken ct)
    {
        var ids = product.FilamentTypes.Select(x => x.FilamentTypeId).ToList();

        return ids.Count == 0
            ? []
            : (await filamentTypes.GetByIdsAsync(ids, ct)).Select(x => x.Code).Order().ToList();
    }
}
