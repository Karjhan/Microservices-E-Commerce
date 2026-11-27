using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ObjectKey).IsRequired();
        builder.Property(x => x.Url).IsRequired();

        builder.Property(x => x.SortOrder).HasDefaultValue(0);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.SizeBytes);

        builder.HasIndex(x => new { x.ProductId, x.SortOrder });

        builder.HasIndex(x => x.ProductId)
            .IsUnique()
            .HasFilter("\"IsPrimary\"")
            .HasDatabaseName("UX_ProductImages_Primary");
    }
}
