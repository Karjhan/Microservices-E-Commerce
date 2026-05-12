using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);

        builder.Property(x => x.ProviderTransactionId).HasMaxLength(200);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);

        builder.HasIndex(x => x.PaymentId);
    }
}
