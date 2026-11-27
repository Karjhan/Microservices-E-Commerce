using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProductFilamentTypeConfiguration : IEntityTypeConfiguration<ProductFilamentType>
{
    public void Configure(EntityTypeBuilder<ProductFilamentType> builder)
    {
        builder.ToTable("ProductFilamentTypes");

        builder.HasKey(x => new { x.ProductId, x.FilamentTypeId });

        // Removing a product removes its rows; a filament type that products use cannot be removed.
        builder.HasOne(x => x.Product)
            .WithMany(x => x.FilamentTypes)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FilamentType)
            .WithMany()
            .HasForeignKey(x => x.FilamentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.FilamentTypeId);
    }
}
