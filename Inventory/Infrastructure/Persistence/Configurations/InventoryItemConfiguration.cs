using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductId).IsRequired();
        builder.HasIndex(x => x.ProductId).IsUnique();

        builder.Property(x => x.OnHandQuantity).IsRequired();
        builder.Property(x => x.ReservedQuantity).IsRequired();

        builder.Ignore(x => x.AvailableQuantity);
        builder.Ignore(x => x.IsDepleted);

        builder.Property(x => x.RowVersion)
            .HasColumnType("bytea")
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
