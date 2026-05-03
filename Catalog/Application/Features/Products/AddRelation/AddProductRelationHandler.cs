using Application.Abstractions.Messaging;
using Contracts.Events;
using Domain.Commons;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.AddRelation;

public class AddProductRelationHandler : IRequestHandler<AddProductRelationCommand, Guid>
{
    private readonly IProductRepository _repo;
    private readonly IEventPublisher _publisher;
    private readonly ICacheService _cache;

    public AddProductRelationHandler(
        IProductRepository repo,
        IEventPublisher publisher,
        ICacheService cache)
    {
        _repo = repo;
        _publisher = publisher;
        _cache = cache;
    }

    public async Task<Guid> Handle(AddProductRelationCommand request, CancellationToken ct)
    {
        if (request.ProductId == request.RelatedProductId)
            throw new ArgumentException("A product cannot be related to itself.");

        if (string.IsNullOrWhiteSpace(request.Type))
            throw new ArgumentException("Relation type is required", nameof(request.Type));

        var product = await _repo.GetByIdAsync(request.ProductId, ct);

        if (product is null)
            throw new KeyNotFoundException("Product not found");

        var relatedProduct = await _repo.GetByIdAsync(request.RelatedProductId, ct);

        if (relatedProduct is null)
            throw new KeyNotFoundException("Related product not found");

        if (product.RelatedProducts.Any(r => r.RelatedProductId == request.RelatedProductId))
            throw new InvalidOperationException("Relation already exists for this product");

        var relation = new ProductRelation(product.Id, request.RelatedProductId, request.Type.Trim());

        product.AddRelatedProduct(relation);
        await _repo.AddRelationAsync(relation, ct);
        await _repo.SaveChangesAsync(ct);

        await _cache.RemoveAsync($"product:{product.Id}");
        await _cache.RemoveByPrefixAsync("products:");

        await _publisher.PublishAsync(new ProductRelationAdded
        {
            ProductId = product.Id,
            RelationId = relation.Id,
            RelatedProductId = relation.RelatedProductId,
            Type = relation.Type
        }, "product.relation.added");

        return relation.Id;
    }
}