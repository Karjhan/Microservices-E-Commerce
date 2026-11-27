using Domain.Entities;

namespace Application.DTOs;

/// <summary><paramref name="ProductCount"/> is only filled for admins, who see how much each entry is used.</summary>
public record BrandDto(
    Guid Id,
    string Name,
    string? LogoUrl,
    bool IsActive,
    int? ProductCount,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static BrandDto FromEntity(Brand brand, int? productCount) =>
        new(brand.Id, brand.Name, brand.LogoUrl, brand.IsActive, productCount, brand.CreatedAt, brand.UpdatedAt);
}

public record CategoryDto(
    Guid Id,
    string Name,
    bool IsActive,
    int? ProductCount,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static CategoryDto FromEntity(Category category, int? productCount) =>
        new(category.Id, category.Name, category.IsActive, productCount, category.CreatedAt, category.UpdatedAt);
}

public record FilamentTypeDto(
    Guid Id,
    string Name,
    string Code,
    decimal? DiameterMm,
    IReadOnlyList<string> ColorOptions,
    bool IsActive,
    int? ProductCount,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static FilamentTypeDto FromEntity(FilamentType filamentType, int? productCount) =>
        new(
            filamentType.Id,
            filamentType.Name,
            filamentType.Code,
            filamentType.DiameterMm,
            filamentType.ColorOptions.ToList(),
            filamentType.IsActive,
            productCount,
            filamentType.CreatedAt,
            filamentType.UpdatedAt);
}

/// <summary>The brand as a product shows it.</summary>
public record ProductBrandDto(Guid Id, string Name, string? LogoUrl);

/// <summary>A filament type as a product shows it; <paramref name="Code"/> is the stable identifier.</summary>
public record ProductFilamentTypeDto(Guid Id, string Name, string Code);
