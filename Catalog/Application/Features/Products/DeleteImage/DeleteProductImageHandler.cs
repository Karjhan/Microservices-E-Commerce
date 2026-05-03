using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Contracts.Events;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.DeleteImage;

public class DeleteProductImageHandler(
    IProductRepository repo,
    IFileStorageService storage,
    IEventPublisher publisher)
    : IRequestHandler<DeleteProductImageCommand>
{
    public async Task Handle(DeleteProductImageCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct);

        if (product is null)
            throw new KeyNotFoundException("Product not found");

        var image = product.Images.FirstOrDefault(x => x.Id == request.ImageId);

        if (image is null)
            throw new KeyNotFoundException("Image not found for this product");

        await storage.DeleteAsync(image.ObjectKey);

        product.Images.Remove(image);

        await repo.SaveChangesAsync(ct);

        await publisher.PublishAsync(new ProductImageDeleted
        {
            ProductId = product.Id,
            ImageId = image.Id,
            ObjectKey = image.ObjectKey
        }, "product.image.deleted");
    }
}