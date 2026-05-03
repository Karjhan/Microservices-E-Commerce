using Application.Abstractions.Messaging;
using Contracts.Events;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.DeleteAttribute;

public class DeleteProductAttributeHandler(
    IProductRepository repo,
    IEventPublisher publisher,
    ICacheService cache)
    : IRequestHandler<DeleteProductAttributeCommand>
{
    public async Task Handle(DeleteProductAttributeCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct);

        if (product is null)
            throw new KeyNotFoundException("Product not found");

        var attribute = product.Attributes.FirstOrDefault(x => x.Id == request.AttributeId);

        if (attribute is null)
            throw new KeyNotFoundException("Attribute not found for this product");

        product.RemoveAttribute(attribute.Id);
        await repo.DeleteAttributeAsync(product.Id, attribute.Id, ct);
        await repo.SaveChangesAsync(ct);

        await cache.RemoveAsync($"product:{product.Id}");
        await cache.RemoveByPrefixAsync("products:");

        await publisher.PublishAsync(new ProductAttributeDeleted
        {
            ProductId = product.Id,
            AttributeId = attribute.Id,
            Key = attribute.Key
        }, "product.attribute.deleted");
    }
}