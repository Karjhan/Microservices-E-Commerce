using Names = Domain.Commons.NormalizedName;

namespace Domain.Entities;

/// <summary>
/// Shared shape of the managed lists (brands, categories, filament types): a unique, case-insensitive name and an
/// active flag. Deactivated entries stay on the products that already use them but cannot be chosen again (FR-021).
/// </summary>
public abstract class ReferenceEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public string Name { get; private set; } = default!;

    /// <summary>Lower-cased, trimmed name with collapsed whitespace; carries the unique index.</summary>
    public string NormalizedName { get; private set; } = default!;

    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    protected ReferenceEntity() { }

    protected ReferenceEntity(string name)
    {
        Id = Guid.NewGuid();
        SetName(name);
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void Rename(string name)
    {
        SetName(name);
        Touch();
    }

    /// <summary>Returns false when the entry already had that state.</summary>
    public bool SetActive(bool active)
    {
        if (IsActive == active)
            return false;

        IsActive = active;
        Touch();
        return true;
    }

    protected void Touch() => UpdatedAt = DateTime.UtcNow;

    private void SetName(string name)
    {
        Name = Names.Clean(name);
        NormalizedName = Names.Of(name);
    }
}
