using Clean_Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Infrastructure.Configuration
{
    public class WorkerConfiguration : IEntityTypeConfiguration<Worker>
    {
        public void Configure(EntityTypeBuilder<Worker> builder)
        {
            builder.HasKey(x => x.Id);

            builder.OwnsOne(w => w.FullName, name =>
            {
                name.Property(n => n.FirstName)
                .HasColumnName("FirstName")
                .IsRequired()
                .HasMaxLength(50);

                name.Property(n => n.LastName)
                .HasColumnName("LastName")
                .IsRequired()
                .HasMaxLength(50);
            });

            builder.OwnsOne(w => w.Email, email =>
            {
                email.Property(e => e.Value)
                .HasColumnName("email")
                .IsRequired()
                .HasMaxLength(100);
            });

            builder.OwnsOne(x => x.Address, address =>
            {
                address.Property(e => e.Value)
                .HasColumnName("address")
                .IsRequired()
                .HasMaxLength(200);
            });

            builder.OwnsOne(x => x.Contact, contact =>
            {
                contact.Property(e => e.Value)
                .HasColumnName("Contact")
                .IsRequired()
                .HasMaxLength(100);
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
            });

            builder.Property(x => x.AverageRating)
                .HasPrecision(3, 2)
                .HasDefaultValue(0);

            builder.Property(x => x.TotalRating)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.State)
                .IsRequired()
                .HasMaxLength(15);

            builder.Property(x => x.IsAvailable)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.DateOfBirth)
                .IsRequired();

            builder.Property(x => x.Gender)
                .HasConversion<string>()
                .IsRequired();
            

            builder.HasOne(x => x.ServiceType)
                .WithMany(x => x.Workers)
                .HasForeignKey(x => x.ServiceTypeId)
                .OnDelete(DeleteBehavior.Restrict);

        }
    }
}
