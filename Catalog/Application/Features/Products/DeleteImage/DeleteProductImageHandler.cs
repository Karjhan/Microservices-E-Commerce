using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Common;
using Contracts.Events;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.DeleteImage;

public class DeleteProductImageHandler(
    IProductRepository repo,
    IFileStorageService storage,
    IEventPublisher publisher,
    IAfterCommit afterCommit,
    ProductCacheInvalidator cache)
    : IRequestHandler<DeleteProductImageCommand>
{
    public async Task Handle(DeleteProductImageCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct)
                      ?? throw new KeyNotFoundException("Product not found");

        var removed = product.RemoveImage(request.ImageId)
                      ?? throw new KeyNotFoundException("Image not found for this product");

        await repo.SaveChangesAsync(ct);

        // When the primary image goes, the next one by sort order takes over (US2 scenario 3).
        // Saved separately so the one-primary index never sees two primaries at once.
        if (removed.IsPrimary && product.PromoteLowestImage() is not null)
            await repo.SaveChangesAsync(ct);

        // The file goes only after the change has committed.
        var objectKey = removed.ObjectKey;
        afterCommit.Register(token => storage.DeleteAsync(StorageBucket.Images, objectKey, token));

        await cache.InvalidateAsync(product.Id);

        await publisher.PublishAsync(new ProductImageDeleted
        {
            ProductId = product.Id,
            ImageId = removed.Id,
            ObjectKey = removed.ObjectKey
        }, "product.image.deleted");
    }
}
