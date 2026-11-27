using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence.Repositories;

public abstract class ReferenceRepository<T>(CatalogDbContext context) : IReferenceRepository<T>
    where T : ReferenceEntity
{
    protected CatalogDbContext Context { get; } = context;

    protected abstract DbSet<T> Set { get; }

    /// <summary>Counts products per entry id; ids with no products may be left out.</summary>
    protected abstract Task<Dictionary<Guid, int>> UsageAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Set.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<T>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        Set.Where(x => ids.Contains(x.Id)).ToListAsync(ct);

    public Task<List<T>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        return query.OrderBy(x => x.NormalizedName).ToListAsync(ct);
    }

    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludeId, CancellationToken ct) =>
        Set.AsNoTracking().AnyAsync(
            x => x.NormalizedName == normalizedName && (excludeId == null || x.Id != excludeId), ct);

    public async Task<Dictionary<Guid, int>> CountProductsUsingAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var counts = await UsageAsync(ids, ct);

        return ids.ToDictionary(id => id, id => counts.GetValueOrDefault(id));
    }

    public async Task AddAsync(T entity, CancellationToken ct) => await Set.AddAsync(entity, ct);

    public void Remove(T entity) => Set.Remove(entity);

    /// <summary>
    /// Saves, turning the database's own uniqueness and foreign-key checks into the same errors the handlers raise.
    /// They only fire when two requests race past the handler checks.
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await Context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            var isCode = pg.ConstraintName?.Contains("Code", StringComparison.OrdinalIgnoreCase) == true;

            throw isCode
                ? new ConflictException("duplicate_code", "That code is already used.", "code", "Code must be unique.")
                : new ConflictException("duplicate_name", "That name is already used.", "name", "Name must be unique.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            throw new ConflictException("in_use", "This entry is used by products. Deactivate it instead.");
        }
    }
}

public class BrandRepository(CatalogDbContext context) : ReferenceRepository<Brand>(context), IBrandRepository
{
    protected override DbSet<Brand> Set => Context.Brands;

    protected override async Task<Dictionary<Guid, int>> UsageAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await Context.Products.AsNoTracking()
            .Where(x => x.BrandId != null && ids.Contains(x.BrandId.Value))
            .GroupBy(x => x.BrandId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
}

public class CategoryRepository(CatalogDbContext context) : ReferenceRepository<Category>(context), ICategoryRepository
{
    protected override DbSet<Category> Set => Context.Categories;

    protected override async Task<Dictionary<Guid, int>> UsageAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await Context.Products.AsNoTracking()
            .Where(x => ids.Contains(x.CategoryId))
            .GroupBy(x => x.CategoryId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
}

public class FilamentTypeRepository(CatalogDbContext context)
    : ReferenceRepository<FilamentType>(context), IFilamentTypeRepository
{
    protected override DbSet<FilamentType> Set => Context.FilamentTypes;

    protected override async Task<Dictionary<Guid, int>> UsageAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await Context.ProductFilamentTypes.AsNoTracking()
            .Where(x => ids.Contains(x.FilamentTypeId))
            .GroupBy(x => x.FilamentTypeId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct)
    {
        var lowered = code.ToLower();

        return Set.AsNoTracking().AnyAsync(
            x => x.Code.ToLower() == lowered && (excludeId == null || x.Id != excludeId), ct);
    }
}
