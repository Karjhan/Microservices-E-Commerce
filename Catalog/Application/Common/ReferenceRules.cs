using Domain.Commons;
using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Abstractions.Persistence;

namespace Application.Common;

/// <summary>Rules shared by brands, categories and filament types (FR-021, FR-023).</summary>
public static class ReferenceRules
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 80;

    /// <summary>Trims and collapses the name, and refuses one that is too short or too long.</summary>
    public static string CleanName(string? name)
    {
        var cleaned = NormalizedName.Clean(name);

        if (cleaned.Length < NameMinLength || cleaned.Length > NameMaxLength)
            throw new ValidationFailedException("name", $"Name must be {NameMinLength} to {NameMaxLength} characters.");

        return cleaned;
    }

    /// <summary>409 <c>duplicate_name</c> when another entry has the same name, ignoring case and spacing.</summary>
    public static async Task EnsureNameAvailableAsync<T>(
        IReferenceRepository<T> repo,
        string cleanedName,
        string label,
        Guid? excludeId,
        CancellationToken ct) where T : ReferenceEntity
    {
        if (await repo.NameExistsAsync(NormalizedName.Of(cleanedName), excludeId, ct))
            throw new ConflictException(
                "duplicate_name",
                $"A {label} named '{cleanedName}' already exists.",
                "name",
                "Name must be unique.");
    }

    /// <summary>409 <c>in_use</c> naming the product count when any product still refers to the entry.</summary>
    public static async Task EnsureNotInUseAsync<T>(
        IReferenceRepository<T> repo,
        T entity,
        string label,
        CancellationToken ct) where T : ReferenceEntity
    {
        var counts = await repo.CountProductsUsingAsync([entity.Id], ct);
        var count = counts[entity.Id];

        if (count == 0)
            return;

        throw new ConflictException(
            "in_use",
            $"The {label} '{entity.Name}' is used by {count} {(count == 1 ? "product" : "products")}. Deactivate it instead.",
            new Dictionary<string, object?> { ["productCount"] = count });
    }
}
