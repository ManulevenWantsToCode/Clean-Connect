using Clean_Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clean_Connect.Infrastructure.Configuration
{
    public class BookingChatConfiguration : IEntityTypeConfiguration<BookingChat>
    {
        public void Configure(EntityTypeBuilder<BookingChat> builder)
        {
            builder.ToTable("BookingChats");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.BookingId)
                .IsRequired();

            builder.Property(x => x.Provider)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.ProviderConversationId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.ClientProviderUserId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.WorkerProviderUserId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.ClientUnreadCount)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.WorkerUnreadCount)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.LastMessageText)
                .HasMaxLength(500);

            builder.Property(x => x.LastMessageSender)
                .HasMaxLength(50);

            builder.Property(x => x.LastMessageSentAt);

            builder.Property(x => x.ClientLastReadAt);

            builder.Property(x => x.WorkerLastReadAt);

            builder.HasIndex(x => x.BookingId)
                .IsUnique();

            builder.HasIndex(x => x.ProviderConversationId)
                .IsUnique();

            builder.HasIndex(x => x.ClientProviderUserId);

            builder.HasIndex(x => x.WorkerProviderUserId);

            builder.HasOne(x => x.Booking)
                .WithOne(x => x.BookingChat)
                .HasForeignKey<BookingChat>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Ignore("DomainEvents");
        }
    }
}
