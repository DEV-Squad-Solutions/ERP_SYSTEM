using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Infrastructure.Services.PayrollEntries
{
    public sealed partial class PayrollEntryService
    {
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
        private static decimal GetRatioValue(WorkDayRatio? ratio)
            {
                return ratio switch
                {
                    WorkDayRatio.FullDay => 1m,
                    WorkDayRatio.ThreeQuarterDay => 0.75m,
                    WorkDayRatio.HalfDay => 0.5m,
                    WorkDayRatio.ThirdDay => 1m / 3m,
                    WorkDayRatio.QuarterDay => 0.25m,
                    _ => 0m
                };
            }
    
    }
}
