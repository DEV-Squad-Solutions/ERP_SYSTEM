using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Employees;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class EmployeeMovementConfiguration
    : AuditableEntityConfiguration<EmployeeMovement>
{
    public override void Configure(EntityTypeBuilder<EmployeeMovement> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "EmployeeMovements",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_EmployeeMovements_Amounts_NonNegative",
                    "[Debit] >= 0 AND [Credit] >= 0");
                table.HasCheckConstraint(
                    "CK_EmployeeMovements_ExactlyOneAmount",
                    "([Debit] > 0 AND [Credit] = 0) OR " +
                    "([Debit] = 0 AND [Credit] > 0)");
            });
        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.Id)
            .ValueGeneratedOnAdd();

        builder.Property(movement => movement.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(movement => new
        {
            movement.CompanyId,
            movement.Id
        });

        builder.Property(movement => movement.EmployeeId)
            .IsRequired();

        builder.Property(movement => movement.CashVoucherId);

        builder.Property(movement => movement.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(movement => movement.MovementDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(movement => movement.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(movement => movement.Debit)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(movement => movement.Credit)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(movement => movement.ExchangeRate)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.RatePrecision,
                Domain.Entities.Companies.ExchangeRateRules.RateScale)
            .HasDefaultValue(1m)
            .IsRequired();

        builder.Property(movement => movement.BaseDebit)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountPrecision,
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(movement => movement.BaseCredit)
            .HasPrecision(
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountPrecision,
                Domain.Entities.Companies.ExchangeRateRules.BaseAmountScale)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(movement => movement.Notes)
            .HasMaxLength(1_000);

        builder.HasIndex(movement => new
            {
                movement.CompanyId,
                movement.CashVoucherId
            })
            .IsUnique()
            .HasFilter("[CashVoucherId] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasIndex(movement => new
        {
            movement.CompanyId,
            movement.EmployeeId,
            movement.Currency,
            movement.MovementDate,
            movement.Id
        });

        builder.HasIndex(movement => new
        {
            movement.CompanyId,
            movement.Type,
            movement.MovementDate
        });

        builder.HasOne(movement => movement.Company)
            .WithMany()
            .HasForeignKey(movement => movement.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Employee)
            .WithMany()
            .HasForeignKey(movement => new
            {
                movement.CompanyId,
                movement.EmployeeId
            })
            .HasPrincipalKey(employee => new
            {
                employee.CompanyId,
                employee.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.CashVoucher)
            .WithMany()
            .HasForeignKey(movement => new
            {
                movement.CompanyId,
                movement.CashVoucherId
            })
            .HasPrincipalKey(voucher => new
            {
                voucher.CompanyId,
                voucher.Id
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
