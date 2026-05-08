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

        var incomingRelations = await repo.RemoveIncomingRelationsAsync(product.Id, ct);

        await repo.DeleteAsync(product, ct);
        await repo.SaveChangesAsync(ct);

        foreach (var attribute in product.Attributes)
        {
            await publisher.PublishAsync(new ProductAttributeDeleted
            {
                ProductId = product.Id,
                AttributeId = attribute.Id,
                Key = attribute.Key
            }, "product.attribute.deleted");
        }

        foreach (var relation in product.RelatedProducts)
        {
            await publisher.PublishAsync(new ProductRelationDeleted
            {
                ProductId = product.Id,
                RelationId = relation.Id,
                RelatedProductId = relation.RelatedProductId,
                Type = relation.Type
            }, "product.relation.deleted");
        }

        foreach (var relation in incomingRelations)
        {
            await publisher.PublishAsync(new ProductRelationDeleted
            {
                ProductId = relation.ProductId,
                RelationId = relation.Id,
                RelatedProductId = product.Id,
                Type = relation.Type
            }, "product.relation.deleted");
        }

        await cache.RemoveAsync($"product:{product.Id}");
        await cache.RemoveByPrefixAsync("products:");

        await publisher.PublishAsync(new ProductDeleted
        {
            ProductId = product.Id
        }, "product.deleted");
    }
}