using Application.Abstractions.Messaging;
using Contracts.Events;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.DeleteRelation;

public class DeleteProductRelationHandler(
    IProductRepository repo,
    IEventPublisher publisher,
    ICacheService cache)
    : IRequestHandler<DeleteProductRelationCommand>
{
    public async Task Handle(DeleteProductRelationCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct);

        if (product is null)
            throw new KeyNotFoundException("Product not found");

        var relation = product.RelatedProducts.FirstOrDefault(x => x.Id == request.RelationId);

        if (relation is null)
            throw new KeyNotFoundException("Relation not found for this product");

        product.RemoveRelatedProduct(relation.Id);
        await repo.DeleteRelationAsync(product.Id, relation.Id, ct);
        await repo.SaveChangesAsync(ct);

        await cache.RemoveAsync($"product:{product.Id}");
        await cache.RemoveByPrefixAsync("products:");

        await publisher.PublishAsync(new ProductRelationDeleted
        {
            ProductId = product.Id,
            RelationId = relation.Id,
            RelatedProductId = relation.RelatedProductId,
            Type = relation.Type
        }, "product.relation.deleted");
    }
}