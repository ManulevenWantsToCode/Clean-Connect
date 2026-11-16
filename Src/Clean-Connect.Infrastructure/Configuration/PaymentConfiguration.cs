namespace Clean_Connect.Infrastructure.Configuration
{
    using global::Clean_Connect.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    namespace Clean_Connect.Persistence.Configurations
    {
        public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
        {
            public void Configure(EntityTypeBuilder<Payment> builder)
            {
                // Table
                builder.ToTable("Payments");

                // Primary Key
                builder.HasKey(p => p.Id);

                // Properties
                builder.Property(p => p.BookingId)
                    .IsRequired();

                builder.Property(p => p.Amount)
                    .IsRequired()
                    .HasColumnType("decimal(18,2)");

                builder.Property(p => p.PaymentMethod)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(p => p.Status)
                    .IsRequired()
                    .HasConversion<string>(); // store enum as string

                builder.Property(p => p.PaymentReference)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(p => p.TransactionId)
                    .HasMaxLength(100);

                builder.Property(p => p.AuthorizationCode)
                    .HasMaxLength(100);

                builder.Property(p => p.Provider)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(p => p.FailureReason)
                    .HasMaxLength(500);

                builder.Property(p => p.IdempotencyKey).HasMaxLength(128);
                builder.Property(p => p.RequestHash).HasMaxLength(64);
                builder.Property(p => p.AuthorizationUrl).HasMaxLength(2048);
                builder.Property(p => p.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("NGN");
                builder.Property(p => p.InitializationStatus).HasConversion<string>().HasMaxLength(32).IsRequired().HasDefaultValue(global::Clean_Connect.Domain.Enums.PaymentInitializationStatus.Pending);
                builder.Property(p => p.InitializationErrorCategory).HasMaxLength(64);
                builder.Property(p => p.RefundStatus).HasMaxLength(30).IsRequired().HasDefaultValue("NotStarted");
                builder.Property(p => p.RefundReference).HasMaxLength(64);
                builder.Property(p => p.RefundAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                builder.Property(p => p.RefundedAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);

                // Indexes (VERY IMPORTANT 🔥)
                builder.HasIndex(p => p.PaymentReference)
                    .IsUnique();

                builder.HasIndex(p => p.TransactionId);

                builder.HasIndex(p => new { p.BookingId, p.Status })
                    .HasFilter("\"Status\" = 'Successful'")
                    .IsUnique();
                builder.HasIndex(p => p.BookingId);

                builder.HasIndex(p => new { p.BookingId, p.IdempotencyKey })
                    .IsUnique()
                    .HasFilter("\"IdempotencyKey\" IS NOT NULL");

                builder.HasIndex(p => p.BookingId)
                    .IsUnique()
                    .HasFilter("\"Status\" = 'Pending'");

                builder.HasIndex(p => p.RefundReference)
                    .IsUnique()
                    .HasFilter("\"RefundReference\" IS NOT NULL");

                builder.HasIndex(p => p.Status);

                // Ignore Domain Events (VERY IMPORTANT)
                builder.Ignore("DomainEvents");

                // Optional: relationships (if Booking exists)
                builder.HasOne<Booking>()
                    .WithMany() // or .WithMany(b => b.Payments) if you add navigation
                    .HasForeignKey(p => p.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
