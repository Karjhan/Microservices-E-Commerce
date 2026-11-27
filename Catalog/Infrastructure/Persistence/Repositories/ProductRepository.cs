using Domain.Commons;
using Domain.Entities;
using Infrastructure.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly CatalogDbContext _context;

    public ProductRepository(CatalogDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Product product, CancellationToken ct)
    {
        await _context.Products.AddAsync(product, ct);
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.Products
            .Include(x => x.Images)
            .Include(x => x.Model)
            .Include(x => x.Attributes)
            .Include(x => x.RelatedProducts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }
    
    public async Task<IReadOnlyList<ProductRelation>> RemoveIncomingRelationsAsync(Guid relatedProductId, CancellationToken ct)
    {
        var relations = await _context.ProductRelations
            .Where(x => x.RelatedProductId == relatedProductId)
            .ToListAsync(ct);

        if (relations.Count > 0)
            _context.ProductRelations.RemoveRange(relations);

        return relations;
    }

    public async Task<List<Product>> GetFilteredAsync(ProductFilter filter, CancellationToken ct)
    {
        var products = await QueryFilteredAsync(filter, ct);

        return products
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();
    }

    public async Task<int> CountAsync(ProductFilter filter, CancellationToken ct)
    {
        var products = await QueryFilteredAsync(filter, ct);
        return products.Count;
    }

    private async Task<List<Product>> QueryFilteredAsync(ProductFilter filter, CancellationToken ct)
    {
        var query = _context.Products
            .AsNoTracking()
            .Include(x => x.Images)
            .Include(x => x.Model)
            .Include(x => x.Attributes)
            .Include(x => x.RelatedProducts)
            .AsSplitQuery()
            .AsQueryable();

        if (filter.CategoryId.HasValue)
            query = query.Where(x => x.CategoryId == filter.CategoryId.Value);

        if (filter.MinPrice.HasValue)
            query = query.Where(x => x.Price >= filter.MinPrice.Value);

        if (filter.MaxPrice.HasValue)
            query = query.Where(x => x.Price <= filter.MaxPrice.Value);

        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search;

            query = query.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%") ||
                EF.Functions.ILike(x.ShortDescription, $"%{search}%"));
        }

        query = query.OrderByDescending(x => x.CreatedAt);

        var products = await query.ToListAsync(ct);

        if (filter.Tags is { Count: > 0 })
        {
            products = products
                .Where(x => x.Tags.Any(t => filter.Tags.Contains(t)))
                .ToList();
        }
        if (filter.Material.HasValue)
        {
            products = products
                .Where(x => x.SupportedMaterials.Contains(filter.Material.Value))
                .ToList();
        }
        if (filter.PrinterType.HasValue)
        {
            products = products
                .Where(x => x.CompatiblePrinters.Contains(filter.PrinterType.Value))
                .ToList();
        }

        return products
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public Task DeleteAsync(Product product, CancellationToken ct)
    {
        _context.Products.Remove(product);
        return Task.CompletedTask;
    }

    public async Task AddImageAsync(ProductImage image, CancellationToken ct)
    {
        await _context.ProductImages.AddAsync(image, ct);
    }

    public async Task AddModelAsync(ProductModel model, CancellationToken ct)
    {
        await _context.ProductModels.AddAsync(model, ct);
    }

    public void RemoveModel(ProductModel model)
    {
        _context.ProductModels.Remove(model);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeProductId, CancellationToken ct)
    {
        var slug = Product.GenerateSlug(name);

        return await _context.Products
            .AsNoTracking()
            .Where(x => excludeProductId == null || x.Id != excludeProductId)
            .AnyAsync(x => EF.Functions.ILike(x.Name, EscapeLike(name)) || x.Slug == slug, ct);
    }

    public async Task<(List<Product> Items, int TotalCount)> GetAdminPageAsync(
        AdminProductFilter filter,
        CancellationToken ct)
    {
        var query = _context.Products.AsNoTracking().AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status.Value);

        if (filter.CategoryId.HasValue)
            query = query.Where(x => x.CategoryId == filter.CategoryId.Value);

        if (filter.ProductIds is not null)
        {
            var ids = filter.ProductIds;
            query = query.Where(x => ids.Contains(x.Id));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{EscapeLike(filter.Search.Trim())}%";

            query = query.Where(x =>
                EF.Functions.ILike(x.Name, pattern) ||
                EF.Functions.ILike(x.ShortDescription, pattern));
        }

        var total = await query.CountAsync(ct);

        query = (filter.SortBy, filter.Descending) switch
        {
            (AdminProductSort.Name, false) => query.OrderBy(x => x.Name),
            (AdminProductSort.Name, true) => query.OrderByDescending(x => x.Name),
            (AdminProductSort.Price, false) => query.OrderBy(x => x.Price),
            (AdminProductSort.Price, true) => query.OrderByDescending(x => x.Price),
            (AdminProductSort.Status, false) => query.OrderBy(x => x.Status),
            (AdminProductSort.Status, true) => query.OrderByDescending(x => x.Status),
            (AdminProductSort.CreatedAt, false) => query.OrderBy(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };

        // A stable tiebreaker keeps pages from overlapping when the sort key repeats.
        var ordered = ((IOrderedQueryable<Product>)query).ThenBy(x => x.Id);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, AdminProductFilter.MaxPageSize);

        var items = await ordered
            .Include(x => x.Images)
            .Include(x => x.Model)
            .Include(x => x.Attributes)
            .Include(x => x.RelatedProducts)
            .AsSplitQuery()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(HashSet<string> ImageKeys, HashSet<string> ModelKeys)> GetReferencedObjectKeysAsync(
        CancellationToken ct)
    {
        var imageKeys = await _context.ProductImages.AsNoTracking().Select(x => x.ObjectKey).ToListAsync(ct);
        var modelKeys = await _context.ProductModels.AsNoTracking().Select(x => x.ObjectKey).ToListAsync(ct);

        return (imageKeys.ToHashSet(), modelKeys.ToHashSet());
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public async Task DeleteImageAsync(Guid productId, Guid imageId, CancellationToken ct)
    {
        var image = await _context.ProductImages
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.Id == imageId, ct);

        if (image is null)
            throw new KeyNotFoundException("Image not found for this product");

        _context.ProductImages.Remove(image);
    }

    public async Task<IReadOnlyList<ProductAttribute>> GetAttributesAsync(Guid productId, CancellationToken ct)
    {
        return await _context.ProductAttributes
            .AsNoTracking()
            .Where(x => x.ProductId == productId)
            .OrderBy(x => x.Key)
            .ToListAsync(ct);
    }

    public async Task AddAttributeAsync(ProductAttribute attribute, CancellationToken ct)
    {
        await _context.ProductAttributes.AddAsync(attribute, ct);
    }

    public async Task DeleteAttributeAsync(Guid productId, Guid attributeId, CancellationToken ct)
    {
        var attribute = await _context.ProductAttributes
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.Id == attributeId, ct);

        if (attribute is null)
            throw new KeyNotFoundException("Attribute not found for this product");

        _context.ProductAttributes.Remove(attribute);
    }

    public async Task<IReadOnlyList<ProductRelation>> GetRelationsAsync(Guid productId, CancellationToken ct)
    {
        return await _context.ProductRelations
            .AsNoTracking()
            .Where(x => x.ProductId == productId)
            .OrderBy(x => x.Type)
            .ThenBy(x => x.RelatedProductId)
            .ToListAsync(ct);
    }

    public async Task AddRelationAsync(ProductRelation relation, CancellationToken ct)
    {
        await _context.ProductRelations.AddAsync(relation, ct);
    }

    public async Task DeleteRelationAsync(Guid productId, Guid relationId, CancellationToken ct)
    {
        var relation = await _context.ProductRelations
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.Id == relationId, ct);

        if (relation is null)
            throw new KeyNotFoundException("Relation not found for this product");

        _context.ProductRelations.Remove(relation);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }
}