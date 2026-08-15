using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Invoicing;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class InvoicePaymentConfiguration
    : AuditableEntityConfiguration<InvoicePayment>
{
    public override void Configure(EntityTypeBuilder<InvoicePayment> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "InvoicePayments",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_InvoicePayments_AppliedAmount_Positive",
                    "[AppliedAmount] > 0");
                table.HasCheckConstraint(
                    "CK_InvoicePayments_CashboxAmount_Positive",
                    "[CashboxAmount] > 0");
                table.HasCheckConstraint(
                    "CK_InvoicePayments_Rates_Positive",
                    "[InvoiceToBaseRate] > 0 AND [CashboxToBaseRate] > 0");
            });

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Id)
            .ValueGeneratedOnAdd();

        builder.Property(payment => payment.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(payment => new
        {
            payment.CompanyId,
            payment.Id
        });

        builder.Property(payment => payment.InvoiceCurrency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(payment => payment.AppliedAmount)
            .HasPrecision(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale)
            .IsRequired();

        builder.Property(payment => payment.CashboxCurrency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(payment => payment.CashboxAmount)
            .HasPrecision(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale)
            .IsRequired();

        builder.Property(payment => payment.InvoiceToBaseRate)
            .HasPrecision(
                ExchangeRateRules.RatePrecision,
                ExchangeRateRules.RateScale)
            .IsRequired();

        builder.Property(payment => payment.CashboxToBaseRate)
            .HasPrecision(
                ExchangeRateRules.RatePrecision,
                ExchangeRateRules.RateScale)
            .IsRequired();

        builder.Property(payment => payment.AppliedBaseAmount)
            .HasPrecision(
                ExchangeRateRules.BaseAmountPrecision,
                ExchangeRateRules.BaseAmountScale)
            .IsRequired();

        builder.Property(payment => payment.CashboxBaseAmount)
            .HasPrecision(
                ExchangeRateRules.BaseAmountPrecision,
                ExchangeRateRules.BaseAmountScale)
            .IsRequired();

        builder.Property(payment => payment.RealizedExchangeDifference)
            .HasPrecision(
                ExchangeRateRules.BaseAmountPrecision,
                ExchangeRateRules.BaseAmountScale)
            .IsRequired();

        builder.HasIndex(payment => new
        {
            payment.CompanyId,
            payment.CashVoucherId
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(payment => new
        {
            payment.CompanyId,
            payment.InvoiceId,
            payment.Id
        });

        builder.HasOne(payment => payment.Company)
            .WithMany()
            .HasForeignKey(payment => payment.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(payment => payment.Invoice)
            .WithMany(invoice => invoice.Payments)
            .HasForeignKey(payment => new
            {
                payment.CompanyId,
                payment.InvoiceId
            })
            .HasPrincipalKey(invoice => new
            {
                invoice.CompanyId,
                invoice.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(payment => payment.CashVoucher)
            .WithOne(voucher => voucher.InvoicePayment)
            .HasForeignKey<InvoicePayment>(payment => new
            {
                payment.CompanyId,
                payment.CashVoucherId
            })
            .HasPrincipalKey<Domain.Entities.CashManagement.CashVoucher>(
                voucher => new
                {
                    voucher.CompanyId,
                    voucher.Id
                })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
