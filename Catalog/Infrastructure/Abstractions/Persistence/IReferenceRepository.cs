using Domain.Entities;

namespace Infrastructure.Abstractions.Persistence;

/// <summary>Operations shared by the managed lists (brands, categories, filament types).</summary>
public interface IReferenceRepository<T> where T : ReferenceEntity
{
    /// <summary>Tracked entity, ready to be changed.</summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<List<T>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Entries ordered by name; inactive ones only when asked for.</summary>
    Task<List<T>> ListAsync(bool includeInactive, CancellationToken ct);

    /// <summary>True when another entry has the same normalized (case-insensitive) name.</summary>
    Task<bool> NameExistsAsync(string normalizedName, Guid? excludeId, CancellationToken ct);

    /// <summary>How many products refer to each entry. Entries nobody uses are present with 0.</summary>
    Task<Dictionary<Guid, int>> CountProductsUsingAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task AddAsync(T entity, CancellationToken ct);
    void Remove(T entity);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IBrandRepository : IReferenceRepository<Brand>;

public interface ICategoryRepository : IReferenceRepository<Category>;

public interface IFilamentTypeRepository : IReferenceRepository<FilamentType>
{
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct);
}
