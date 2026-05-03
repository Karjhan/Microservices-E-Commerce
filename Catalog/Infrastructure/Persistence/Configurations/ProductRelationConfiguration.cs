using Domain.Commons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProductRelationConfiguration : IEntityTypeConfiguration<ProductRelation>
{
    public void Configure(EntityTypeBuilder<ProductRelation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).IsRequired().HasMaxLength(50);

        builder.HasIndex(x => new { x.ProductId, x.RelatedProductId, x.Type }).IsUnique();
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.RelatedProductId);
    }
}