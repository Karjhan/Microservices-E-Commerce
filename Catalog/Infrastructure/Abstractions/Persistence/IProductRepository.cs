using Domain.Commons;
using Domain.Entities;

namespace Infrastructure.Abstractions.Persistence;

public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken ct);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<Product>> GetFilteredAsync(ProductFilter filter, CancellationToken ct);

    Task AddImageAsync(ProductImage image, CancellationToken ct);
    Task AddModelAsync(ProductModel model, CancellationToken ct);
    void RemoveModel(ProductModel model);

    /// <summary>True when another product already has this name (case-insensitive) or the same slug.</summary>
    Task<bool> NameExistsAsync(string name, Guid? excludeProductId, CancellationToken ct);

    Task<(List<Product> Items, int TotalCount)> GetAdminPageAsync(AdminProductFilter filter, CancellationToken ct);

    /// <summary>Every storage key the database still refers to, for the orphan sweep.</summary>
    Task<(HashSet<string> ImageKeys, HashSet<string> ModelKeys)> GetReferencedObjectKeysAsync(CancellationToken ct);

    Task<int> CountAsync(ProductFilter filter, CancellationToken ct);

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