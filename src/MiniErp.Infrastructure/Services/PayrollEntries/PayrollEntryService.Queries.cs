using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.PayrollEntries;

public sealed partial class PayrollEntryService
{
    private static IOrderedQueryable<PayrollEntry> ApplyFilters(
        IQueryable<PayrollEntry> baseQuery,
        PayrollEntryFilterRequest filters)
    {
        var search = filters.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            baseQuery = baseQuery.Where(e =>
                e.EmployeeCode.Contains(search) ||
                e.EmployeeName.Contains(search));
        }

        if (filters.StartDate.HasValue)
            baseQuery = baseQuery.Where(e => e.StartDate >= filters.StartDate.Value);

        if (filters.EndDate.HasValue)
            baseQuery = baseQuery.Where(e => e.EndDate <= filters.EndDate.Value);

        if (filters.EmployeeId.HasValue)
            baseQuery = baseQuery.Where(e => e.EmployeeId == filters.EmployeeId.Value);

        if (filters.EmployeeType.HasValue)
            baseQuery = baseQuery.Where(e => e.EmployeeType == filters.EmployeeType.Value);
        return baseQuery
            .OrderByDescending(e => e.EndDate)
            .ThenBy(e => e.EmployeeName)
            .ThenBy(e => e.Id);
    }

    private async Task<AttendanceSummary> GetAttendanceSummaryAsync(
        int employeeId,
        int companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var attendances = await dbContext.EmployeeAttendances
            .Where(a =>
                a.EmployeeId == employeeId &&
                a.CompanyId == companyId &&
                a.WorkDate >= startDate &&
                a.WorkDate <= endDate)
            .Select(a => new
            {
                a.Status,
                a.WorkDayRatio,
                a.WorkOverTimeRatio,
                a.WorkDaysDeductionRatio
            })
            .ToListAsync(cancellationToken);

        return new AttendanceSummary(
            PresentDays: attendances.Count(a => a.Status == AttendanceStatus.Present),
            AbsentDays: attendances.Count(a => a.Status == AttendanceStatus.Absent),
            TotalPresentDays: attendances
                .Where(a => a.Status == AttendanceStatus.Present)
                .Sum(a => GetRatioValue(a.WorkDayRatio)),
            TotalOvertimeDays: attendances
                .Where(a => a.Status == AttendanceStatus.Present)
                .Sum(a => GetRatioValue(a.WorkOverTimeRatio)),
            TotalDeductionDays: attendances
                .Where(a => a.Status == AttendanceStatus.Present)
                .Sum(a => GetRatioValue(a.WorkDaysDeductionRatio)));
    }

    private static decimal GetRatioValue(WorkDayRatio? ratio) =>
        ratio switch
        {
            WorkDayRatio.FullDay => 1m,
            WorkDayRatio.ThreeQuarterDay => 0.75m,
            WorkDayRatio.HalfDay => 0.5m,
            WorkDayRatio.ThirdDay => 1m / 3m,
            WorkDayRatio.QuarterDay => 0.25m,
            _ => 0m
        };
}
