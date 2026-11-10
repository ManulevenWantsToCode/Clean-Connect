using Clean_Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clean_Connect.Infrastructure.Configuration;

public class PlatformSettingsConfiguration : IEntityTypeConfiguration<PlatformSettings>
{
    public void Configure(EntityTypeBuilder<PlatformSettings> builder)
    {
        builder.ToTable("PlatformSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SupportEmail).HasMaxLength(254).IsRequired();
        builder.Property(x => x.CustomerServicePhone).HasMaxLength(32).IsRequired();
        builder.Property(x => x.BaseCurrency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Timezone).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CommissionPercent).HasPrecision(5, 2);
        builder.Property(x => x.MinimumPayoutAmount).HasPrecision(12, 2);
        builder.HasData(new PlatformSettings { Id = 1 });
    }
}
