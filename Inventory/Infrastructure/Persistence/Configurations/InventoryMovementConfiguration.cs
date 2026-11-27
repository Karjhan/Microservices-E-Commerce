using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductId).IsRequired();
        builder.Property(x => x.MovementType).IsRequired();
        builder.Property(x => x.QuantityDelta).IsRequired();
        builder.Property(x => x.OnHandAfter).IsRequired();
        builder.Property(x => x.ReservedAfter).IsRequired();

        builder.Property(x => x.SourceTrigger)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.Property(x => x.ActorEmail).HasMaxLength(256);

        builder.Property(x => x.OccurredAt).IsRequired();

        // The history of one product, newest first.
        builder.HasIndex(x => new { x.ProductId, x.OccurredAt }).IsDescending(false, true);
        builder.HasIndex(x => x.OrderId);
    }
}
