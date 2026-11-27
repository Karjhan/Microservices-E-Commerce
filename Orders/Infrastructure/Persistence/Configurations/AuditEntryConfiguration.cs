using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Security.Audit;

namespace Infrastructure.Persistence.Configurations;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries", t => t.HasCheckConstraint(
            "CK_AuditEntries_Outcome",
            "\"Outcome\" IN ('success', 'refused', 'failed')"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OccurredAt).IsRequired();
        builder.Property(x => x.ActorEmail).HasMaxLength(256);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(100);
        builder.Property(x => x.TargetType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.TargetId).HasMaxLength(300);
        builder.Property(x => x.Outcome).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Details).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        builder.HasIndex(x => x.OccurredAt).IsDescending();
    }
}
