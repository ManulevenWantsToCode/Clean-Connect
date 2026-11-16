using Clean_Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clean_Connect.Infrastructure.Configuration;

public sealed class PaystackWebhookInboxEventConfiguration : IEntityTypeConfiguration<PaystackWebhookInboxEvent>
{
    public void Configure(EntityTypeBuilder<PaystackWebhookInboxEvent> builder)
    {
        builder.ToTable("PaystackWebhookInboxEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProviderEventId).HasMaxLength(100);
        builder.Property(x => x.PaymentReference).HasMaxLength(100);
        builder.Property(x => x.SafePayload).HasColumnType("text").IsRequired();
        builder.Property(x => x.ProcessingStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.LastErrorCategory).HasMaxLength(64);
        builder.Property(x => x.LastRequeuedBy).HasMaxLength(100);
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.HasIndex(x => x.EventKey).IsUnique();
        builder.HasIndex(x => new { x.ProcessingStatus, x.ReceivedAtUtc });
        builder.HasIndex(x => new { x.ProcessingStatus, x.NextAttemptAtUtc, x.ReceivedAtUtc });
    }
}
