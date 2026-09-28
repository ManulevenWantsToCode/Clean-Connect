using Clean_Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clean_Connect.Infrastructure.Configuration
{
    public class WorkerBankDetailConfiguration : IEntityTypeConfiguration<WorkerBankDetail>
    {
        public void Configure(EntityTypeBuilder<WorkerBankDetail> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.BankCode)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.BankName)
                .HasMaxLength(100);

            builder.Property(x => x.AccountNumber)
                .IsRequired()
                .HasMaxLength(30);

            builder.Property(x => x.AccountName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Currency)
                .IsRequired()
                .HasMaxLength(5)
                .HasDefaultValue("NGN");

            builder.Property(x => x.RecipientCode)
                .HasMaxLength(100);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(x => x.WorkerId)
                .IsUnique()
                .HasFilter("\"IsActive\"");

            builder.HasOne(x => x.Worker)
                .WithOne()
                .HasForeignKey<WorkerBankDetail>(x => x.WorkerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}