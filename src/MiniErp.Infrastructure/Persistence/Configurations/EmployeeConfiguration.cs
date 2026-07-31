using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Employees;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : AuditableEntityConfiguration<Employee>
{
    public override void Configure(EntityTypeBuilder<Employee> builder)
    {
        base.Configure(builder);

        builder.ToTable("Employees", table =>
        {
            table.HasCheckConstraint(
                "CK_Employees_SalaryRates_NonNegative",
                "([DailyRate] IS NULL OR [DailyRate] >= 0) AND ([MonthlySalary] IS NULL OR [MonthlySalary] >= 0)");
        });

        builder.HasKey(employee => employee.Id);
        builder.Property(employee => employee.Id).ValueGeneratedOnAdd();

        builder.HasAlternateKey(employee => new { employee.CompanyId, employee.Id });

        builder.Property(e => e.Code)
            .HasComputedColumnSql(
                "'Emp-' + RIGHT('00000' + CAST([EmployeeNumber] AS VARCHAR(5)), 5)",
                stored: true);

        builder.HasIndex(employee => new { employee.CompanyId, employee.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(employee => employee.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(employee => new { employee.CompanyId, employee.Name })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(employee => employee.PhoneNumber).HasMaxLength(50);
        builder.Property(employee => employee.Email).HasMaxLength(256);
        builder.Property(employee => employee.Address).HasMaxLength(500);

        builder.Property(employee => employee.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(employee => employee.DailyRate)
            .HasPrecision(18,2);

        builder.Property(employee => employee.MonthlySalary)
            .HasPrecision(18,2);

        builder.HasOne(employee => employee.Company)
            .WithMany()
            .HasForeignKey(employee => employee.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}