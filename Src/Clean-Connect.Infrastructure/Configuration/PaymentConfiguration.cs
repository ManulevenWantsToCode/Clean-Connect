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

                // Indexes (VERY IMPORTANT 🔥)
                builder.HasIndex(p => p.PaymentReference)
                    .IsUnique();

                builder.HasIndex(p => p.TransactionId);

                builder.HasIndex(p => new { p.BookingId, p.Status })
                    .HasFilter("\"Status\" = 'Successful'")
                    .IsUnique();
                builder.HasIndex(p => p.BookingId);

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
