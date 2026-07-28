using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Logistics;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class DriverTripConfiguration : AuditableEntityConfiguration<DriverTrip>
{
    public override void Configure(EntityTypeBuilder<DriverTrip> builder)
    {
        base.Configure(builder);

        builder.ToTable("DriverTrips");
        builder.HasKey(trip => trip.Id);

        builder.Property(trip => trip.Id)
            .ValueGeneratedOnAdd();

        builder.Property(trip => trip.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(trip => new
        {
            trip.CompanyId,
            trip.Id
        });

        builder.Property(trip => trip.DriverId)
            .IsRequired();

        builder.HasIndex(trip => new
        {
            trip.CompanyId,
            trip.ActualDriverId
        });

        builder.Property(trip => trip.InvoiceId)
            .IsRequired();

        builder.Property(trip => trip.BusinessPartnerId)
            .IsRequired();

        builder.Property(trip => trip.InvoiceNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(trip => trip.ExportInvoiceCode)
            .HasMaxLength(100);

        builder.Property(trip => trip.TripDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(trip => trip.Price)
            .HasPrecision(18, 2);

        builder.Property(trip => trip.Cost)
            .HasPrecision(18, 2);

        builder.Property(trip => trip.CostNotes)
            .HasMaxLength(1_000);

        builder.Property(trip => trip.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(trip => new
        {
            trip.CompanyId,
            trip.InvoiceId
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(trip => new
        {
            trip.CompanyId,
            trip.DriverId,
            trip.TripDate,
            trip.Id
        });

        builder.HasOne(trip => trip.Company)
            .WithMany()
            .HasForeignKey(trip => trip.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(trip => trip.Driver)
            .WithMany()
            .HasForeignKey(trip => new
            {
                trip.CompanyId,
                trip.DriverId
            })
            .HasPrincipalKey(driver => new
            {
                driver.CompanyId,
                driver.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(trip => trip.ActualDriver)
            .WithMany()
            .HasForeignKey(trip => new
            {
                trip.CompanyId,
                trip.ActualDriverId
            })
            .HasPrincipalKey(driver => new
            {
                driver.CompanyId,
                driver.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(trip => trip.Invoice)
            .WithMany()
            .HasForeignKey(trip => new
            {
                trip.CompanyId,
                trip.InvoiceId
            })
            .HasPrincipalKey(invoice => new
            {
                invoice.CompanyId,
                invoice.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(trip => trip.BusinessPartner)
            .WithMany()
            .HasForeignKey(trip => new
            {
                trip.CompanyId,
                trip.BusinessPartnerId
            })
            .HasPrincipalKey(partner => new
            {
                partner.CompanyId,
                partner.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
