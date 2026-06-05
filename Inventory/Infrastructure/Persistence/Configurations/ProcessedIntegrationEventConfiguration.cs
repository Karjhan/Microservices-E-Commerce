using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProcessedIntegrationEventConfiguration : IEntityTypeConfiguration<ProcessedIntegrationEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedIntegrationEvent> builder)
    {
        builder.ToTable("ProcessedIntegrationEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ConsumerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ProcessedAt).IsRequired();

        builder.HasIndex(x => new { x.ConsumerName, x.IdempotencyKey }).IsUnique();
    }
}
