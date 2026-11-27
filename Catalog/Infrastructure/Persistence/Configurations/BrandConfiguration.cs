using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        ReferenceEntityConfiguration.ConfigureCommon(builder, "UX_Brands_NormalizedName");

        builder.Property(x => x.LogoObjectKey).HasMaxLength(300);
        builder.Property(x => x.LogoUrl).HasMaxLength(1000);

        // Deleting a brand is refused while products use it; if one is removed anyway the products simply lose the brand.
        builder.HasMany<Product>()
            .WithOne(x => x.Brand)
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
