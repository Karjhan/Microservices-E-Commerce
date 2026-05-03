using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Contracts.Events;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.DeleteProduct;

public class DeleteProductHandler(
    IProductRepository repo,
    IFileStorageService storage,
    IEventPublisher publisher,
    ICacheService cache)
    : IRequestHandler<DeleteProductCommand>
{
    public async Task Handle(DeleteProductCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct);

        if (product is null)
            throw new KeyNotFoundException("Product not found");

        foreach (var image in product.Images)
        {
            await storage.DeleteAsync(image.ObjectKey);
        }

        await repo.DeleteAsync(product, ct);
        await repo.SaveChangesAsync(ct);

        await cache.RemoveAsync($"product:{product.Id}");
        await cache.RemoveByPrefixAsync("products:");

        await publisher.PublishAsync(new ProductDeleted
        {
            ProductId = product.Id
        }, "product.deleted");
    }
}