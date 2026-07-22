using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class DriverConfiguration : AuditableEntityConfiguration<Driver>
{
    public override void Configure(EntityTypeBuilder<Driver> builder)
    {
        base.Configure(builder);

        builder.ToTable("Drivers");
        builder.HasKey(driver => driver.Id);

        builder.Property(driver => driver.Id)
            .ValueGeneratedOnAdd();

        builder.Property(driver => driver.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(driver => new { driver.CompanyId, driver.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(driver => driver.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(driver => new { driver.CompanyId, driver.Name })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(driver => driver.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(driver => driver.NationalId)
            .HasMaxLength(50);

        builder.HasIndex(driver => new { driver.CompanyId, driver.NationalId })
            .IsUnique()
            .HasFilter("[NationalId] IS NOT NULL AND [IsDeleted] = 0");

        builder.Property(driver => driver.LicenseNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(driver => new { driver.CompanyId, driver.LicenseNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(driver => driver.LicenseExpiryDate)
            .HasColumnType("date");

        builder.HasOne(driver => driver.Company)
            .WithMany()
            .HasForeignKey(driver => driver.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
