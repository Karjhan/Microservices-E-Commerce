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
            .Include(x => x.Attributes)
            .Include(x => x.RelatedProducts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<List<Product>> GetFilteredAsync(ProductFilter filter, CancellationToken ct)
    {
        var query = _context.Products
            .AsNoTracking()
            .Include(x => x.Images)
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

        products = products
            .OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return products;
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