using Application.Abstractions.Messaging;
using Contracts.Events;
using Domain.Commons;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.AddAttribute;

public class AddProductAttributeHandler(
    IProductRepository repo,
    IEventPublisher publisher,
    ICacheService cache)
    : IRequestHandler<AddProductAttributeCommand, Guid>
{
    public async Task<Guid> Handle(AddProductAttributeCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            throw new ArgumentException("Attribute key is required", nameof(request.Key));

        if (string.IsNullOrWhiteSpace(request.Value))
            throw new ArgumentException("Attribute value is required", nameof(request.Value));

        var product = await repo.GetByIdAsync(request.ProductId, ct);

        if (product is null)
            throw new KeyNotFoundException("Product not found");

        if (product.Attributes.Any(a => a.Key == request.Key))
            throw new InvalidOperationException("Attribute key already exists for this product");

        var attribute = new ProductAttribute(product.Id, request.Key.Trim(), request.Value.Trim());

        product.AddAttribute(attribute);
        await repo.AddAttributeAsync(attribute, ct);
        await repo.SaveChangesAsync(ct);

        await cache.RemoveAsync($"product:{product.Id}");
        await cache.RemoveByPrefixAsync("products:");

        await publisher.PublishAsync(new ProductAttributeAdded
        {
            ProductId = product.Id,
            AttributeId = attribute.Id,
            Key = attribute.Key,
            Value = attribute.Value
        }, "product.attribute.added");

        return attribute.Id;
    }
}