using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ShippingAddressConfiguration : IEntityTypeConfiguration<ShippingAddress>
{
    public void Configure(EntityTypeBuilder<ShippingAddress> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Street).HasMaxLength(200).IsRequired();
        builder.Property(x => x.City).HasMaxLength(100).IsRequired();
        builder.Property(x => x.State).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Country).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ZipCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(20);
    }
}
