using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Employees;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration(bool isSqlite)
    : AuditableEntityConfiguration<Employee>
{
    public override void Configure(EntityTypeBuilder<Employee> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "Employees",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Employees_Salary_NonNegative",
                    "([DailySalary] IS NULL OR [DailySalary] >= 0) AND " +
                    "([MonthlySalary] IS NULL OR [MonthlySalary] >= 0)");

                table.HasCheckConstraint(
                    "CK_Employees_SalaryType",
                    "([Type] = 1 AND [DailySalary] IS NOT NULL AND [MonthlySalary] IS NULL) OR " +
                    "([Type] = 2 AND [MonthlySalary] IS NOT NULL AND [DailySalary] IS NULL)");

                table.HasCheckConstraint(
                    "CK_Employees_RequiredWorkingDays",
                    "[RequiredWorkingDaysPerMonth] IS NULL OR " +
                    "([RequiredWorkingDaysPerMonth] >= 1 AND [RequiredWorkingDaysPerMonth] <= 31)");
            });

        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.Id)
            .ValueGeneratedOnAdd();

        builder.Property(employee => employee.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(employee => new
        {
            employee.CompanyId,
            employee.Id
        });

        if (isSqlite)
        {
            builder.Property(employee => employee.Code)
                .HasComputedColumnSql("'Emp-' || SUBSTR('000' || Id, -3, 3)")
                .IsUnicode();
        }
        else
        {
            builder.Property(employee => employee.Code)
                .HasComputedColumnSql(
                    "N'Emp-' + RIGHT(N'000' + CAST([Id] AS NVARCHAR(10)), 3)",
                    stored: true)
                .IsUnicode();
        }

        builder.HasIndex(employee => new
        {
            employee.CompanyId,
            employee.Code
        })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(employee => employee.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(employee => new
        {
            employee.CompanyId,
            employee.Name
        })
            .HasFilter("[IsDeleted] = 0");

        builder.Property(employee => employee.JobTitle)
            .HasMaxLength(100);

        builder.Property(employee => employee.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(employee => employee.Email)
            .HasMaxLength(256);

        builder.Property(employee => employee.Address)
            .HasMaxLength(500);

        builder.Property(employee => employee.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(employee => employee.DailySalary)
            .HasPrecision(18, 2);

        builder.Property(employee => employee.MonthlySalary)
            .HasPrecision(18, 2);

        builder.Property(employee => employee.RequiredWorkingDaysPerMonth);

        builder.Property(employee => employee.LastDayOfReceivingSalary)
            .HasColumnType("date");

        builder.Property(employee => employee.IsActive)
            .IsRequired();

        builder.HasOne(employee => employee.Company)
            .WithMany()
            .HasForeignKey(employee => new
            {
                employee.CompanyId
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}