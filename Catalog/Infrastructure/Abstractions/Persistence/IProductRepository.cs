using Domain.Commons;
using Domain.Entities;

namespace Infrastructure.Abstractions.Persistence;

public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken ct);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<Product>> GetFilteredAsync(ProductFilter filter, CancellationToken ct);

    Task AddImageAsync(ProductImage image, CancellationToken ct);

    Task<IReadOnlyList<ProductAttribute>> GetAttributesAsync(Guid productId, CancellationToken ct);
    Task AddAttributeAsync(ProductAttribute attribute, CancellationToken ct);
    Task DeleteAttributeAsync(Guid productId, Guid attributeId, CancellationToken ct);

    Task<IReadOnlyList<ProductRelation>> GetRelationsAsync(Guid productId, CancellationToken ct);
    Task AddRelationAsync(ProductRelation relation, CancellationToken ct);
    Task DeleteRelationAsync(Guid productId, Guid relationId, CancellationToken ct);
    Task<IReadOnlyList<ProductRelation>> RemoveIncomingRelationsAsync(Guid relatedProductId, CancellationToken ct);

    Task DeleteAsync(Product product, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}