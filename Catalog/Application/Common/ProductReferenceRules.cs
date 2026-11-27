using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Abstractions.Persistence;

namespace Application.Common;

/// <summary>
/// Checks the brand, category and filament types a product is saved with. Entries must exist and be active when they
/// are newly assigned; ones the product already has stay valid after being deactivated (US3 scenario 3).
/// </summary>
public static class ProductReferenceRules
{
    public static async Task EnsureCategoryAsync(
        ICategoryRepository repo,
        Guid categoryId,
        Guid? currentCategoryId,
        CancellationToken ct)
    {
        var category = (await repo.GetByIdsAsync([categoryId], ct)).FirstOrDefault()
                       ?? throw new ValidationFailedException("categoryId", "The category does not exist.");

        EnsureAssignable(category, "category", categoryId == currentCategoryId);
    }

    public static async Task EnsureBrandAsync(
        IBrandRepository repo,
        Guid? brandId,
        Guid? currentBrandId,
        CancellationToken ct)
    {
        if (brandId is null)
            return;

        var brand = (await repo.GetByIdsAsync([brandId.Value], ct)).FirstOrDefault()
                    ?? throw new ValidationFailedException("brandId", "The brand does not exist.");

        EnsureAssignable(brand, "brand", brandId == currentBrandId);
    }

    /// <summary>Returns the distinct ids, after checking each exists and is active unless the product already has it.</summary>
    public static async Task<List<Guid>> EnsureFilamentTypesAsync(
        IFilamentTypeRepository repo,
        IEnumerable<Guid> filamentTypeIds,
        IReadOnlyCollection<Guid> currentIds,
        CancellationToken ct)
    {
        var ids = filamentTypeIds.Distinct().ToList();

        if (ids.Count == 0)
            return ids;

        var found = (await repo.GetByIdsAsync(ids, ct)).ToDictionary(x => x.Id);

        foreach (var id in ids)
        {
            if (!found.TryGetValue(id, out var filamentType))
                throw new ValidationFailedException("filamentTypeIds", "One of the filament types does not exist.");

            EnsureAssignable(filamentType, "filament type", currentIds.Contains(id));
        }

        return ids;
    }

    private static void EnsureAssignable(ReferenceEntity entry, string label, bool alreadyAssigned)
    {
        if (!entry.IsActive && !alreadyAssigned)
            throw new BusinessRuleException(
                "reference_inactive",
                $"The {label} '{entry.Name}' is deactivated and cannot be assigned to a product.");
    }
}
