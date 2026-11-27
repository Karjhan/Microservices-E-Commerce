using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class FilamentTypeConfiguration : IEntityTypeConfiguration<FilamentType>
{
    public void Configure(EntityTypeBuilder<FilamentType> builder)
    {
        builder.ToTable("FilamentTypes");
        ReferenceEntityConfiguration.ConfigureCommon(builder, "UX_FilamentTypes_NormalizedName");

        builder.Property(x => x.Code).IsRequired().HasMaxLength(FilamentType.CodeMaxLength);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_FilamentTypes_Code");

        builder.Property(x => x.DiameterMm).HasPrecision(4, 2);

        builder.Property(x => x.ColorOptions)
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null)
                     ?? new List<string>())
            .Metadata.SetValueComparer(
                new ValueComparer<List<string>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                    v => v.ToList()));
    }
}
