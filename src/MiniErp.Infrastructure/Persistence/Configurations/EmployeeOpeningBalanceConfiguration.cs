using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Employees;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class EmployeeOpeningBalanceConfiguration :
    AuditableEntityConfiguration<EmployeeOpeningBalance>
{
    public override void Configure(EntityTypeBuilder<EmployeeOpeningBalance> builder)
    {
        base.Configure(builder);
        builder.ToTable("EmployeeOpeningBalances", table =>
        {
            table.HasCheckConstraint(
                "CK_EmployeeOpeningBalances_Currency_EGP",
                "[Currency] = 1");
            table.HasCheckConstraint(
                "CK_EmployeeOpeningBalances_Amount_Positive",
                "[Amount] > 0");
        });
        builder.HasKey(balance => balance.Id);

        builder.Property(balance => balance.Id)
            .ValueGeneratedOnAdd();

        builder.Property(balance => balance.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(balance => new
        {
            balance.CompanyId,
            balance.Id
        });

        builder.Property(balance => balance.EmployeeId)
            .IsRequired();

        builder.Property(balance => balance.PayrollEntryId);

        builder.Property(balance => balance.DocumentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(balance => balance.DocumentDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(balance => balance.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(balance => balance.ExchangeRateId);

        builder.Property(balance => balance.ExchangeRate)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.RatePrecision,
                Domain.Entities.Companies.ExchangeRateRules.RateScale)
            .HasDefaultValue(1m)
            .IsRequired();

        builder.Property(balance => balance.BalanceType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(balance => balance.Amount)
            .HasPrecision(
                PartnerOpeningBalanceAmountRules.MoneyPrecision,
                PartnerOpeningBalanceAmountRules.MoneyScale)
            .IsRequired();

        builder.Property(balance => balance.BaseAmount)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountPrecision,
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(balance => balance.Notes)
            .HasMaxLength(1_000);

        builder.Property(balance => balance.RowVersion)
            .IsRowVersion();

        builder.HasIndex(balance => new
            {
                balance.CompanyId,
                balance.DocumentNumber
            })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(balance => new
            {
                balance.CompanyId,
                balance.PayrollEntryId
            })
            .IsUnique()
            .HasFilter("[PayrollEntryId] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasOne(balance => balance.Company)
            .WithMany()
            .HasForeignKey(balance => balance.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(balance => balance.ExchangeRateRecord)
            .WithMany()
            .HasForeignKey(balance => new
            {
                balance.CompanyId,
                balance.ExchangeRateId
            })
            .HasPrincipalKey(rate => new
            {
                rate.CompanyId,
                rate.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(balance => balance.Employee)
            .WithMany()
            .HasForeignKey(balance => new
            {
                balance.CompanyId,
                balance.EmployeeId
            })
            .HasPrincipalKey(employee => new
            {
                employee.CompanyId,
                employee.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(balance => balance.PayrollEntry)
            .WithMany()
            .HasForeignKey(balance => new
            {
                balance.CompanyId,
                balance.PayrollEntryId
            })
            .HasPrincipalKey(entry => new
            {
                entry.CompanyId,
                entry.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
