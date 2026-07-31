using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Payroll;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class PayrollEntryConfiguration
    : AuditableEntityConfiguration<PayrollEntry>
{
    public override void Configure(EntityTypeBuilder<PayrollEntry> builder)
    {
        base.Configure(builder);

        builder.ToTable("PayrollEntries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .ValueGeneratedOnAdd();

        builder.HasAlternateKey(entry => new
        {
            entry.CompanyId,
            entry.Id
        });

        builder.Property(entry => entry.PayrollPeriodId)
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

        builder.Property(entry => entry.DailyRateApplied)
            .HasPrecision(18, 2);

        builder.Property(entry => entry.MonthlySalaryApplied)
            .HasPrecision(18, 2);

        builder.Property(entry => entry.OvertimeHours)
            .HasPrecision(18, 2);

        builder.Property(entry => entry.TotalCredits)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entry => entry.TotalDebits)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entry => entry.GrossSalary)
            .HasPrecision(18, 2);

        builder.Property(entry => entry.NetSalary)
            .HasPrecision(18, 2);

        builder.HasOne(entry => entry.Company)
            .WithMany()
            .HasForeignKey(entry => entry.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entry => entry.PayrollPeriod)
            .WithMany()
            .HasForeignKey(entry => new
            {
                entry.CompanyId,
                entry.PayrollPeriodId
            })
            .HasPrincipalKey(period => new
            {
                period.CompanyId,
                period.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

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

        // Ensure an employee only has one entry per payroll period
        builder.HasIndex(entry => new { entry.CompanyId, entry.PayrollPeriodId, entry.EmployeeId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}