using Clean_Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clean_Connect.Infrastructure.Configuration
{
    public class EscrowConfiguration : IEntityTypeConfiguration<Escrow>
    {
        public void Configure(EntityTypeBuilder<Escrow> builder)
        {
            builder.ToTable("Escrows");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.BookingId)
                .IsRequired();

            builder.Property(x => x.PaymentId)
                .IsRequired();

            builder.Property(x => x.WorkerId)
                .IsRequired();

            builder.Property(x => x.Amount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(x => x.CommissionRate)
                .HasColumnType("decimal(18,2)")
                .IsRequired()
                .HasDefaultValue(10m);

            builder.Property(x => x.CommissionAmount)
                .HasColumnType("decimal(18,2)")
                .IsRequired()
                .HasDefaultValue(0m);

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(x => x.DateReleased);

            builder.Property(x => x.PaystackTransferCode)
                .HasMaxLength(100);

            builder.Property(x => x.DatePaidOut);

            builder.HasIndex(x => x.BookingId)
                .IsUnique();

            builder.HasIndex(x => x.PaymentId)
                .IsUnique();

            builder.HasIndex(x => new { x.WorkerId, x.Status });

            builder.HasOne(x => x.Booking)
                .WithOne()
                .HasForeignKey<Escrow>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Payment)
                .WithOne()
                .HasForeignKey<Escrow>(x => x.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Worker)
                .WithMany()
                .HasForeignKey(x => x.WorkerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Ignore("DomainEvents");
        }
    }
}
