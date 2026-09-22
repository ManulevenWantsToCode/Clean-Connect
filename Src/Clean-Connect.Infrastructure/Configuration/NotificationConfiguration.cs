using Clean_Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clean_Connect.Infrastructure.Configuration
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.HasKey(n => n.Id);

            builder.Property(n => n.Audience)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(n => n.TargetId)
                .IsRequired();

            builder.Property(n => n.BookingId)
                .IsRequired();

            builder.Property(n => n.Title)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(n => n.Message)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(n => n.Status)
                .HasMaxLength(50);

            builder.Property(n => n.Tone)
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(n => n.Icon)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(n => n.ActionText)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(n => n.ActionUrl)
                .HasMaxLength(500);

            builder.Property(n => n.NeedsAttention)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(n => n.IsRead)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(n => n.ReadAt);

            builder.HasIndex(n => new { n.TargetId, n.Audience });
            builder.HasIndex(n => n.BookingId);
        }
    }
}