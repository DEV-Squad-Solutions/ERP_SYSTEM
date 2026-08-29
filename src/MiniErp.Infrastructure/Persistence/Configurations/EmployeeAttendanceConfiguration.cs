using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Infrastructure.Persistence.Configurations;

public sealed class EmployeeAttendanceConfiguration
    : AuditableEntityConfiguration<EmployeeAttendance>
{
    public override void Configure(
        EntityTypeBuilder<EmployeeAttendance> builder)
    {
        base.Configure(builder);

        builder.ToTable(
            "EmployeeAttendances",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_EmployeeAttendances_EmployeeAttendanceStatus",
                    "[Status] IN (0,1)");
                table.HasCheckConstraint(
                    "CK_EmployeeAttendances_WorkDayRatio",
                    "[WorkDayRatio] IN (1,2,3,4,5)");
                table.HasCheckConstraint(
                    "CK_EmployeeAttendances_WorkOverTimeRatio",
                    "[WorkOverTimeRatio] IS NULL OR [WorkOverTimeRatio] IN (1,2,3,4,5)");
                table.HasCheckConstraint(
                    "CK_EmployeeAttendances_WorkDaysDeductionRatio",
                    "[WorkDaysDeductionRatio] IS NULL OR [WorkDaysDeductionRatio] IN (1,2,3,4,5)");
            });

        builder.HasKey(attendance => attendance.Id);

        builder.Property(attendance => attendance.Id)
            .ValueGeneratedOnAdd();

        builder.Property(attendance => attendance.CompanyId)
            .IsRequired();

        builder.HasAlternateKey(attendance => new
        {
            attendance.CompanyId,
            attendance.Id
        });

        builder.Property(attendance => attendance.EmployeeId)
            .IsRequired();

        builder.Property(attendance => attendance.Status)
            .HasColumnName("Status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(attendance => attendance.WorkDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(attendance => attendance.CheckIn)
            .HasColumnType("time")
            .IsRequired(false);

        builder.Property(attendance => attendance.CheckOut)
            .HasColumnType("time")
            .IsRequired(false);

        builder.Property(attendance => attendance.WorkHours)
            .HasColumnType("time")
            .IsRequired(false);

        builder.Property(attendance => attendance.WorkDayRatio)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(attendance => attendance.WorkOverTimeRatio)
            .HasConversion<int>()
            .IsRequired(false);

        builder.Property(attendance => attendance.WorkDaysDeductionRatio)
            .HasConversion<int>()
            .IsRequired(false);

        builder.Property(attendance => attendance.WorkLocation)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(attendance => attendance.Notes)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.HasIndex(attendance => new
        {
            attendance.CompanyId,
            attendance.EmployeeId,
            attendance.WorkDate
        })
        .IsUnique()
        .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(attendance => new
        {
            attendance.CompanyId,
            attendance.WorkDate
        });

        builder.HasIndex(attendance => new
        {
            attendance.CompanyId,
            attendance.EmployeeId,
            attendance.Status
        });

        builder.HasOne(attendance => attendance.Company)
            .WithMany()
            .HasForeignKey(attendance => attendance.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(attendance => attendance.Employee)
            .WithMany()
            .HasForeignKey(attendance => new
            {
                attendance.CompanyId,
                attendance.EmployeeId
            })
            .HasPrincipalKey(employee => new
            {
                employee.CompanyId,
                employee.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
