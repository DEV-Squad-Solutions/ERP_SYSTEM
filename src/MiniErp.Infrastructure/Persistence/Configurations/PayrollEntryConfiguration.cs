using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Payroll;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class PayrollEntryConfiguration
    : AuditableEntityConfiguration<PayrollEntry>
{
    public override void Configure(EntityTypeBuilder<PayrollEntry> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "PayrollEntries",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_PayrollEntries_Dates",
                    "[StartDate] <= [EndDate]");

                table.HasCheckConstraint(
                    "CK_PayrollEntries_Days",
                    "[PresentDays] >= 0 AND " +
                    "[AbsentDays] >= 0 AND " +
                    "[WorkedDaysbydayunit] >= 0");

                table.HasCheckConstraint(
                    "CK_PayrollEntries_Amounts_NonNegative",
                    "([Overtimebydayunit] IS NULL OR [Overtimebydayunit] >= 0) AND " +
                    "([Deductionbydayunit] IS NULL OR [Deductionbydayunit] >= 0) AND " +
                    "([RequiredWorkingDays] IS NULL OR [RequiredWorkingDays] >= 0) AND " +
                    "([Bonus] IS NULL OR [Bonus] >= 0) AND " +
                    "([Deduction] IS NULL OR [Deduction] >= 0) AND " +
                    "([SalaryPerDay] IS NULL OR [SalaryPerDay] >= 0) AND " +
                    "[CalculatedSalary] >= 0 AND " +
                    "[GrossSalary] >= 0 AND " +
                    "[NetSalary] >= 0");
            });

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .ValueGeneratedOnAdd();

        builder.HasAlternateKey(entry => new
        {
            entry.CompanyId,
            entry.Id
        });


        builder.Property(entry => entry.CompanyId)
            .IsRequired();

        builder.HasOne(entry => entry.Company)
            .WithMany()
            .HasForeignKey(entry => entry.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(entry => entry.StartDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(entry => entry.EndDate)
            .HasColumnType("date")
            .IsRequired();

       builder.Property(entry => entry.EmployeeId)
            .IsRequired();

        builder.Property(entry => entry.EmployeeCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.EmployeeName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entry => entry.EmployeeType)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(entry => entry.Employee)
            .WithMany()
            .HasForeignKey(entry => new
            {
                entry.CompanyId,
                entry.EmployeeId
            })
            .HasPrincipalKey(employee => new
            {
                employee.CompanyId,
                employee.Id
            })
            .OnDelete(DeleteBehavior.Restrict);


        builder.Property(entry => entry.PresentDays)
            .IsRequired();

        builder.Property(entry => entry.AbsentDays)
            .IsRequired();

        builder.Property(entry => entry.WorkedDaysbydayunit)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entry => entry.Overtimebydayunit)
            .HasPrecision(18, 2);

        builder.Property(entry => entry.Deductionbydayunit)
            .HasPrecision(18, 2);

        builder.Property(entry => entry.RequiredWorkingDays)
            .HasPrecision(18, 2);

        builder.Property(entry => entry.Bonus)
            .HasPrecision(18, 2);

        builder.Property(entry => entry.Deduction)
            .HasPrecision(18, 2);


        builder.Property(entry => entry.SalaryPerDay)
            .HasPrecision(18, 2);

        builder.Property(entry => entry.CalculatedSalary)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entry => entry.GrossSalary)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entry => entry.NetSalary)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entry => entry.IsSalaryMoveToEmployeeAccount)
            .HasColumnName("IsTakeSalary")
            .IsRequired();

        builder.Property(entry => entry.SalaryMovedOn)
            .HasColumnType("date");

        // ============================================================
        // Indexes
        // ============================================================

        // Prevent duplicate payroll entries for the same employee
        // and payroll period.
        builder.HasIndex(entry => new
        {
            entry.CompanyId,
            entry.EmployeeId,
            entry.StartDate,
            entry.EndDate
        })
        .IsUnique()
        .HasFilter("[IsDeleted] = 0");

        // Useful for querying payroll by period.
        builder.HasIndex(entry => new
        {
            entry.CompanyId,
            entry.StartDate,
            entry.EndDate
        });

        // Useful for querying payroll by employee type.
        builder.HasIndex(entry => new
        {
            entry.CompanyId,
            entry.EmployeeType
        });

        // Useful for finding payroll records by employee.
        builder.HasIndex(entry => new
        {
            entry.CompanyId,
            entry.EmployeeId
        });
    }
}