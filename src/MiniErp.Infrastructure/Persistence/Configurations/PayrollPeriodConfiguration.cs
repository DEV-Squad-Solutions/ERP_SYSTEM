using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Payroll;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class PayrollPeriodConfiguration
    : AuditableEntityConfiguration<PayrollPeriod>
{
    public override void Configure(EntityTypeBuilder<PayrollPeriod> builder)
    {
        base.Configure(builder);

        builder.ToTable("PayrollPeriods");

        builder.HasKey(period => period.Id);

        builder.Property(period => period.Id)
            .ValueGeneratedOnAdd();

        builder.HasAlternateKey(period => new
        {
            period.CompanyId,
            period.Id
        });
        builder.Property(e => e.Code)
            .HasComputedColumnSql(
                "N'Roll-' + RIGHT(N'000' + CAST([Id] AS NVARCHAR(10)), 3)",
                stored: true)
            .IsUnicode();

        builder.Property(period => period.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(period => new { period.CompanyId, period.Name })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(period => period.StartDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(period => period.EndDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(period => period.TotalEmployees)
            .IsRequired();

        builder.Property(period => period.TotalMonthlyEmployees)
            .IsRequired();

        builder.Property(period =>period.TotalDailyEmployees)
            .IsRequired();

        builder.Property(period => period.TotalDebits)
            .IsRequired();

        builder.Property(period => period.TotalCredits)
            .IsRequired();

        builder.Property(period => period.TotalNetSalary)
            .IsRequired();

        builder.Property(period => period.TotalGrossSalary)
            .IsRequired();

        builder.Property(period => period.TotalWorkedDays)
            .IsRequired();

        builder.Property(period => period.TotalAbsentDays)
            .IsRequired();

        builder.Property(period => period.TotalOvertimeDays)
            .IsRequired();

        builder.Property(period => period.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(period => period.Company)
            .WithMany()
            .HasForeignKey(period => period.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}