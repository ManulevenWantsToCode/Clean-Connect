using Clean_Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Infrastructure.Configuration
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.HasKey(x => x.Id);

            builder.OwnsOne(c => c.Address, address =>
            {
                address.Property(a => a.Value)
                    .HasColumnName("Address")
                    .IsRequired()
                    .HasMaxLength(200);
            });

            builder.OwnsOne(b => b.Location, loc =>
            {
                loc.Property(l => l.Latitude)
                .IsRequired()
                .HasColumnType("decimal(9,6)")
                .HasColumnName("Latitude");

                loc.Property(l => l.Longitude)
                .IsRequired()
                .HasColumnType("decimal(9,6)")
                .HasColumnName("Longitude");

                loc.Property(l => l.Point)
                .HasColumnType("geography")
                .HasColumnName("LocationPoint");

                loc.HasIndex(l => l.Point)
                .HasDatabaseName("IX_Bookings_LocationPoint");
                

            });

            


            builder.Property(x => x.DateOfBooking)
                .IsRequired();

            builder.Property(x => x.DateOfService)
                .IsRequired();

            builder.Property(x => x.StartTime)
                .IsRequired();

            builder.Property(x => x.EndTime)
                .IsRequired();

            builder.Property(x => x.HourlyRate)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.TotalAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.TimeRange)
                .HasConversion<string>()
                .IsRequired();

            builder.Property(x => x.PaymentStatus)
                .HasConversion<string>()
                .IsRequired();


            builder.Property(x => x.BookingStatus)
                .HasConversion<string>()
                .IsRequired();

            builder.Property(x => x.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.HasOne(x => x.ServiceType)
                .WithMany()
                .HasForeignKey(x => x.ServiceTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Worker)
                .WithMany(x => x.Bookings)
                .HasForeignKey(x => x.WorkerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Client)
                .WithMany(x => x.Bookings)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);



        }
    }
}
