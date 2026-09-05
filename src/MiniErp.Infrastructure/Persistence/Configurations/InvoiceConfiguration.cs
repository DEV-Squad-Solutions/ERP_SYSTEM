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
        });

        builder.Property(invoice => invoice.ExportInvoiceCode)
            .HasMaxLength(100);

        builder.Property(invoice => invoice.PartnerInvoiceNo)
            .HasMaxLength(100);

        builder.Property(invoice => invoice.InvoiceType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(invoice => invoice.ContentType)
            .HasConversion<int>()
            .HasDefaultValue(Domain.Enums.InvoiceContentType.Items)
            .HasSentinel(Domain.Enums.InvoiceContentType.Items)
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

        builder.Property(invoice => invoice.ExchangeRateId);

        builder.Property(invoice => invoice.ExchangeRate)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.RatePrecision,
                Domain.Entities.Companies.ExchangeRateRules.RateScale)
            .HasDefaultValue(1m)
            .IsRequired();

        builder.Property(invoice => invoice.UsesExternalDriver)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(invoice => invoice.ExternalDriverName)
            .HasMaxLength(200);

        builder.Property(invoice => invoice.ActualDriverName)
            .HasMaxLength(200);

        builder.Property(invoice => invoice.VehicleNumber)
            .HasMaxLength(100);

        builder.Property(invoice => invoice.DiscountAmount)
            .HasPrecision(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(invoice => invoice.WBWeight)
            .HasPrecision(
                InvoiceAmountRules.QuantityPrecision,
                InvoiceAmountRules.QuantityScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(invoice => invoice.WBScaleDifference)
            .HasPrecision(
                InvoiceAmountRules.QuantityPrecision,
                InvoiceAmountRules.QuantityScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(invoice => invoice.WBDiscount)
            .HasPrecision(
                InvoiceAmountRules.QuantityPrecision,
                InvoiceAmountRules.QuantityScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(invoice => invoice.WBTotal)
            .HasPrecision(
                InvoiceAmountRules.QuantityPrecision,
                InvoiceAmountRules.QuantityScale)
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

        builder.Property(invoice => invoice.BaseSubtotal)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountPrecision,
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(invoice => invoice.BaseDiscountAmount)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountPrecision,
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(invoice => invoice.BaseTotal)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountPrecision,
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(invoice => invoice.BasePaidAmountAtInvoiceRate)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountPrecision,
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Ignore(invoice => invoice.Subtotal);
        builder.Ignore(invoice => invoice.RemainingAmount);
        builder.Ignore(invoice => invoice.PaymentStatus);

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

        builder.HasIndex(invoice => new
        {
            invoice.CompanyId,
            invoice.InvoiceDate,
            invoice.InvoiceType
        })
            .HasDatabaseName("IX_Invoices_Company_Date_Type");

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

        builder.HasOne(invoice => invoice.ItemsCategory)
            .WithMany(category => category.Invoices)
            .HasForeignKey(invoice => new
            {
                invoice.CompanyId,
                invoice.ItemsCategoryId
            })
            .HasPrincipalKey(category => new
            {
                category.CompanyId,
                category.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(invoice => new
        {
            invoice.CompanyId,
            invoice.ItemsCategoryId
        });

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

        builder.HasOne(invoice => invoice.ExchangeRateRecord)
            .WithMany()
            .HasForeignKey(invoice => new
            {
                invoice.CompanyId,
                invoice.ExchangeRateId
            })
            .HasPrincipalKey(rate => new
            {
                rate.CompanyId,
                rate.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(invoice => new
        {
            invoice.CompanyId,
            invoice.ExchangeRateId
        });

    }
}
