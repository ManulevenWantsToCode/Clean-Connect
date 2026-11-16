using Clean_Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clean_Connect.Infrastructure.Configuration;

public sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("AuditRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorId).HasMaxLength(100);
        builder.Property(x => x.ActorKind).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(16).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ChangedFields).HasColumnType("text").IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAtUtc });
    }
}
