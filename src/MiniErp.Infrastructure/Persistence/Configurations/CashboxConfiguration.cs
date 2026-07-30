using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class CashboxConfiguration
    : AuditableEntityConfiguration<Cashbox>
{
    public override void Configure(EntityTypeBuilder<Cashbox> builder)
    {
        base.Configure(builder);

        builder.ToTable("Cashboxes");
        builder.HasKey(cashbox => cashbox.Id);

        builder.Property(cashbox => cashbox.Id)
            .ValueGeneratedOnAdd();

        builder.Property(cashbox => cashbox.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(cashbox => new
        {
            cashbox.CompanyId,
            cashbox.Id
        });

        builder.Property(cashbox => cashbox.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(cashbox => new
        {
            cashbox.CompanyId,
            cashbox.Code
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(cashbox => cashbox.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(cashbox => new
        {
            cashbox.CompanyId,
            cashbox.Name
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(cashbox => cashbox.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(cashbox => cashbox.OpeningBalance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(cashbox => cashbox.OpeningBalanceDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(cashbox => cashbox.OpeningExchangeRateId);

        builder.Property(cashbox => cashbox.OpeningExchangeRate)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.RatePrecision,
                Domain.Entities.Companies.ExchangeRateRules.RateScale)
            .HasDefaultValue(1m)
            .IsRequired();

        builder.Property(cashbox => cashbox.BaseOpeningBalance)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountPrecision,
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(cashbox => cashbox.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(cashbox => cashbox.Notes)
            .HasMaxLength(1_000);

        builder.Property(cashbox => cashbox.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(cashbox => new
        {
            cashbox.CompanyId,
            cashbox.IsActive,
            cashbox.Name,
            cashbox.Id
        });

        builder.HasOne(cashbox => cashbox.Company)
            .WithMany()
            .HasForeignKey(cashbox => cashbox.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cashbox => cashbox.OpeningExchangeRateRecord)
            .WithMany()
            .HasForeignKey(cashbox => new
            {
                cashbox.CompanyId,
                cashbox.OpeningExchangeRateId
            })
            .HasPrincipalKey(rate => new
            {
                rate.CompanyId,
                rate.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
