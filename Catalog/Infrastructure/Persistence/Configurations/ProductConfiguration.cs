using Domain.Commons;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(200);

        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.Status);

        builder.Property(x => x.ShortDescription).HasMaxLength(500);
        builder.Property(x => x.LongDescription);

        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(10);

        builder.Property(x => x.DownloadCount).HasDefaultValue(0);
        builder.Property(x => x.AverageRating).HasPrecision(5, 2);

        builder.OwnsOne(x => x.Settings, settings =>
        {
            settings.Property(p => p.LayerHeight);
            settings.Property(p => p.InfillPercentage);
            settings.Property(p => p.NozzleSize);
            settings.Property(p => p.PrintTimeMinutes);
            settings.Property(p => p.FilamentUsedGrams);
            settings.Property(p => p.SupportsRequired);
        });

        builder.OwnsOne(x => x.Size, size =>
        {
            size.Property(p => p.WidthMm);
            size.Property(p => p.HeightMm);
            size.Property(p => p.DepthMm);
        });

        builder.Property(x => x.Tags)
            .HasConversion(
                v => string.Join(',', v),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<string>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            .Metadata.SetValueComparer(
                new ValueComparer<List<string>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                    v => v.ToList()));

        builder.Property(x => x.SupportedMaterials)
            .HasConversion(
                v => string.Join(',', v.Select(e => e.ToString())),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<MaterialType>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => Enum.Parse<MaterialType>(x))
                        .ToList())
            .Metadata.SetValueComparer(
                new ValueComparer<List<MaterialType>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                    v => v.ToList()));

        builder.Property(x => x.CompatiblePrinters)
            .HasConversion(
                v => string.Join(',', v.Select(e => e.ToString())),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<PrinterType>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => Enum.Parse<PrinterType>(x))
                        .ToList())
            .Metadata.SetValueComparer(
                new ValueComparer<List<PrinterType>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                    v => v.ToList()));

        builder.HasMany(x => x.Images)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Attributes)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.RelatedProducts)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}