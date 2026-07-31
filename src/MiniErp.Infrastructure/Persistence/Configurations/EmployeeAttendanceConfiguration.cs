using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Entities.Employees;

namespace MiniErp.Infrastructure.Persistence.Configurations;

public sealed class EmployeeAttendanceConfiguration
    : AuditableEntityConfiguration<EmployeeAttendance>
{
    public override void Configure(EntityTypeBuilder<EmployeeAttendance> builder)
    {
        base.Configure(builder);

        builder.ToTable("EmployeeAttendances");

        builder.HasKey(attendance => attendance.Id);

        builder.Property(attendance => attendance.Id)
            .ValueGeneratedOnAdd();

        builder.HasAlternateKey(attendance => new
        {
            attendance.CompanyId,
            attendance.Id
        });

        builder.Property(attendance => attendance.EmployeeId)
            .IsRequired();

        builder.Property(attendance => attendance.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(attendance => attendance.WorkDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(attendance => attendance.CheckIn)
            .HasColumnType("time");

        builder.Property(attendance => attendance.CheckOut)
            .HasColumnType("time");

        builder.Property(attendance => attendance.WorkHours)
            .HasPrecision(18, 2);

        builder.Property(attendance => attendance.WorkDayRatio)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(attendance => attendance.WorkOverTimeRatio)
            .HasConversion<int>();
            

        builder.Property(attendance => attendance.WorkDaysDeductionRatio)
            .HasConversion<int>();
            

        builder.Property(attendance => attendance.WorkLocation)
            .HasMaxLength(200);

        builder.Property(attendance => attendance.Notes)
            .HasMaxLength(500);

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