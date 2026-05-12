using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(10);

        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Method).IsRequired();

        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();

        builder.Property(x => x.PaymentToken).HasMaxLength(300);

        builder.Property(x => x.ProviderPaymentId).HasMaxLength(200);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);

        builder.Property(x => x.RefundedAmount).HasPrecision(18, 2);

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);

        builder.HasMany(x => x.Transactions)
            .WithOne(x => x.Payment)
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
