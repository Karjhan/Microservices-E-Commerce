using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

/// <summary>Columns and the case-insensitive unique name shared by brands, categories and filament types (FR-023).</summary>
internal static class ReferenceEntityConfiguration
{
    public const int NameMaxLength = 80;

    public static void ConfigureCommon<T>(EntityTypeBuilder<T> builder, string uniqueIndexName) where T : ReferenceEntity
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(NameMaxLength);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(NameMaxLength);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        // NormalizedName is stored trimmed and lower-cased, so a plain unique index is case-insensitive.
        builder.HasIndex(x => x.NormalizedName).IsUnique().HasDatabaseName(uniqueIndexName);
    }
}
