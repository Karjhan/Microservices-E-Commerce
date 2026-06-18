using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_documents_idempotency_key");

        builder.HasIndex(x => x.OrderId).HasDatabaseName("IX_documents_order_id");
        builder.HasIndex(x => x.PaymentId).HasDatabaseName("IX_documents_payment_id");
        builder.HasIndex(x => x.UserId).HasDatabaseName("IX_documents_user_id");
        builder.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("IX_documents_status_created_at");

        builder.Property(x => x.TemplateVersion).HasMaxLength(32).IsRequired();
        builder.Property(x => x.StorageObjectKey).HasMaxLength(512);
        builder.Property(x => x.StorageBucket).HasMaxLength(128);
        builder.Property(x => x.ContentType).HasMaxLength(64);
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.Property(x => x.SourceRoutingKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(1024);
        builder.Property(x => x.RequestContractJson).HasColumnType("jsonb");

        builder.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
    }
}
