using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProductModelConfiguration : IEntityTypeConfiguration<ProductModel>
{
    public void Configure(EntityTypeBuilder<ProductModel> builder)
    {
        builder.ToTable("ProductModels", t =>
        {
            t.HasCheckConstraint("CK_ProductModels_Format", "\"Format\" IN ('glb','gltf','stl','3mf','obj')");
            t.HasCheckConstraint("CK_ProductModels_Size", "\"SizeBytes\" <= 104857600");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ObjectKey).IsRequired();
        builder.Property(x => x.OriginalFilename).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Format).IsRequired().HasMaxLength(10);

        builder.HasIndex(x => x.ProductId).IsUnique();

        builder.HasOne(x => x.Product)
            .WithOne(x => x.Model)
            .HasForeignKey<ProductModel>(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
