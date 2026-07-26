using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Invoicing;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : AuditableEntityConfiguration<Invoice>
{
    public override void Configure(EntityTypeBuilder<Invoice> builder)
    {
        base.Configure(builder);

        builder.ToTable("Invoices");
        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.Id)
            .ValueGeneratedOnAdd();

        builder.Property(invoice => invoice.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(invoice => new
        {
            invoice.CompanyId,
            invoice.Id
        });

        builder.Property(invoice => invoice.InvoiceNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(invoice => new
        {
            invoice.CompanyId,
            invoice.InvoiceNumber
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(invoice => invoice.ExportInvoiceCode)
            .HasMaxLength(100);

        builder.Property(invoice => invoice.InvoiceType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(invoice => invoice.PaymentTerm)
            .HasConversion<int>()
            .HasDefaultValue(Domain.Enums.PaymentTerm.Cash)
            .HasSentinel(Domain.Enums.PaymentTerm.Cash)
            .IsRequired();

        builder.Property(invoice => invoice.InvoiceDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(invoice => invoice.DueDate)
            .HasColumnType("date");

        builder.Property(invoice => invoice.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(invoice => invoice.UsesExternalDriver)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(invoice => invoice.ExternalDriverName)
            .HasMaxLength(200);

        builder.Property(invoice => invoice.VehicleNumber)
            .HasMaxLength(100);

        builder.Property(invoice => invoice.DiscountAmount)
            .HasPrecision(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(invoice => invoice.PaidAmount)
            .HasPrecision(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(invoice => invoice.Total)
            .HasPrecision(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale)
            .IsRequired();

        builder.Ignore(invoice => invoice.Subtotal);
        builder.Ignore(invoice => invoice.RemainingAmount);

        builder.Property(invoice => invoice.Notes)
            .HasMaxLength(1_000);

        builder.Property(invoice => invoice.LastModifiedAt)
            .HasColumnType("datetime2(7)")
            .IsRequired();

        builder.Property(invoice => invoice.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(invoice => new
        {
            invoice.CompanyId,
            invoice.BusinessPartnerId,
            invoice.InvoiceDate
        });

        builder.HasOne(invoice => invoice.Company)
            .WithMany()
            .HasForeignKey(invoice => invoice.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(invoice => invoice.BusinessPartner)
            .WithMany()
            .HasForeignKey(invoice => new
            {
                invoice.CompanyId,
                invoice.BusinessPartnerId
            })
            .HasPrincipalKey(partner => new
            {
                partner.CompanyId,
                partner.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(invoice => invoice.Store)
            .WithMany()
            .HasForeignKey(invoice => new
            {
                invoice.CompanyId,
                invoice.StoreId
            })
            .HasPrincipalKey(store => new
            {
                store.CompanyId,
                store.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(invoice => invoice.ContainerStore)
            .WithMany()
            .HasForeignKey(invoice => new
            {
                invoice.CompanyId,
                invoice.ContainerStoreId
            })
            .HasPrincipalKey(store => new
            {
                store.CompanyId,
                store.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(invoice => invoice.Country)
            .WithMany()
            .HasForeignKey(invoice => invoice.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(invoice => invoice.Driver)
            .WithMany()
            .HasForeignKey(invoice => new
            {
                invoice.CompanyId,
                invoice.DriverId
            })
            .HasPrincipalKey(driver => new
            {
                driver.CompanyId,
                driver.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(invoice => invoice.Lines)
            .WithOne(line => line.Invoice)
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.InvoiceId
            })
            .HasPrincipalKey(invoice => new
            {
                invoice.CompanyId,
                invoice.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(invoice => invoice.ContainerLines)
            .WithOne(line => line.Invoice)
            .HasForeignKey(line => new
            {
                line.CompanyId,
                line.InvoiceId
            })
            .HasPrincipalKey(invoice => new
            {
                invoice.CompanyId,
                invoice.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
