using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Payroll;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class PayrollPeriodConfiguration
    : AuditableEntityConfiguration<PayrollPeriod>
{
    public override void Configure(
        EntityTypeBuilder<PayrollPeriod> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "PayrollPeriods",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_PayrollPeriods_Dates",
                    "[StartDate] <= [EndDate]");

                table.HasCheckConstraint(
                    "CK_PayrollPeriods_WorkingDays",
                    "[WorkingDaysInPeriod] > 0");

                table.HasCheckConstraint(
                    "CK_PayrollPeriods_Amounts",
                    "([TotalGrossSalary] IS NULL OR [TotalGrossSalary] >= 0) AND " +
                    "([TotalCredits] IS NULL OR [TotalCredits] >= 0) AND " +
                    "([TotalDebits] IS NULL OR [TotalDebits] >= 0) AND " +
                    "([TotalNetSalary] IS NULL OR [TotalNetSalary] >= 0) AND " +
                    "([TotalWorkedDays] IS NULL OR [TotalWorkedDays] >= 0) AND " +
                    "([TotalOvertimeDays] IS NULL OR [TotalOvertimeDays] >= 0) AND " +
                    "([TotalAbsentDays] IS NULL OR [TotalAbsentDays] >= 0)");

                table.HasCheckConstraint(
                    "CK_PayrollPeriods_EmployeeCounts",
                    "([TotalEmployees] IS NULL OR [TotalEmployees] >= 0) AND " +
                    "([TotalMonthlyEmployees] IS NULL OR [TotalMonthlyEmployees] >= 0) AND " +
                    "([TotalDailyEmployees] IS NULL OR [TotalDailyEmployees] >= 0)");
            });

        builder.HasKey(period => period.Id);

        builder.Property(period => period.Id)
            .ValueGeneratedOnAdd();

        builder.Property(period => period.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(period => new
        {
            period.CompanyId,
            period.Id
        });

        builder.Property(period => period.Code)
            .HasComputedColumnSql(
                "N'Roll-' + RIGHT(N'000' + CAST([Id] AS NVARCHAR(10)), 3)",
                stored: true)
            .IsUnicode();

        builder.HasIndex(period => new
        {
            period.CompanyId,
            period.Code
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(period => period.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(period => new
        {
            period.CompanyId,
            period.Name
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(period => period.StartDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(period => period.EndDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(period => period.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(period => period.WorkingDaysInPeriod)
            .IsRequired();

        builder.Property(period => period.TotalEmployees);

        builder.Property(period => period.TotalMonthlyEmployees);

        builder.Property(period => period.TotalDailyEmployees);

        builder.Property(period => period.TotalGrossSalary)
            .HasPrecision(18, 2);

        builder.Property(period => period.TotalCredits)
            .HasPrecision(18, 2);

        builder.Property(period => period.TotalDebits)
            .HasPrecision(18, 2);

        builder.Property(period => period.TotalNetSalary)
            .HasPrecision(18, 2);

        builder.Property(period => period.TotalWorkedDays)
            .HasPrecision(18, 2);

        builder.Property(period => period.TotalOvertimeDays)
            .HasPrecision(18, 2);

        builder.Property(period => period.TotalAbsentDays)
            .HasPrecision(18, 2);

        builder.Property(period => period.CalculatedAt);

        builder.Property(period => period.PaidAt);

        builder.HasIndex(period => new
        {
            period.CompanyId,
            period.StartDate,
            period.EndDate
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(period => new
        {
            period.CompanyId,
            period.Status
        });

        builder.HasOne(period => period.Company)
            .WithMany()
            .HasForeignKey(period => period.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}