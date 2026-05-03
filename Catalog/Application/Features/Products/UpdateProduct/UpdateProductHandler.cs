using Application.Abstractions.Messaging;
using Contracts.Events;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.UpdateProduct;

public class UpdateProductHandler(
    IProductRepository repo,
    IEventPublisher publisher,
    ICacheService cache)
    : IRequestHandler<UpdateProductCommand>
{
    public async Task Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct);

        if (product is null)
            throw new KeyNotFoundException("Product not found");

        product.UpdateDetails(
            request.Name,
            request.ShortDescription,
            request.LongDescription,
            request.Price
        );

        if (request.DownloadCount is not null)
        {
            product.SetDownloadCount((int)request.DownloadCount);
        }

        if (request.AverageRating is not null)
        {
            product.SetAverageRating((double)request.AverageRating);
        }

        product.SetCurrency(request.Currency);
        product.SetSettings(request.Settings);
        product.SetSize(request.Size);

        if (request.Tags is not null)
            product.SetTags(request.Tags);

        if (request.SupportedMaterials is not null)
            product.SetSupportedMaterials(request.SupportedMaterials);

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
            SupportedMaterials = product.SupportedMaterials.Select(x => x.ToString()).ToList(),
            CompatiblePrinters = product.CompatiblePrinters.Select(x => x.ToString()).ToList(),
            UpdatedAt = product.UpdatedAt ?? DateTime.UtcNow
        }, "product.updated");
    }
}