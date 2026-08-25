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
        if (filters.IsSalaryMoveToEmployeeAccount.HasValue)
            baseQuery = baseQuery.Where(e => e.IsSalaryMoveToEmployeeAccount == filters.IsSalaryMoveToEmployeeAccount.Value);
        return baseQuery
            .OrderByDescending(e => e.EndDate)
            .ThenByDescending(e => e.Id);
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
            WorkDayRatio.FullDay         => 1m,
            WorkDayRatio.ThreeQuarterDay => 0.75m,
            WorkDayRatio.HalfDay         => 0.5m,
            WorkDayRatio.ThirdDay        => 1m / 3m,
            WorkDayRatio.QuarterDay      => 0.25m,
            _                            => 0m
        };

    /// <summary>
    /// Computes (grossSalary, calculatedSalary) from an employee and their attendance summary.
    /// Returns (-1, -1) when the employee's salary configuration is incomplete.
    /// </summary>
    private static (decimal GrossSalary, decimal CalculatedSalary) CalculateSalary(
        Domain.Entities.Employees.Employee employee,
        AttendanceSummary summary)
    {
        var workedUnits = summary.TotalPresentDays
            + (summary.TotalOvertimeDays  ?? 0m)
            - (summary.TotalDeductionDays ?? 0m);

        if (employee.Type == EmployeeType.Monthly && employee.MonthlySalary.HasValue)
        {
            var gross = employee.MonthlySalary.Value;
            var calculated = employee.RequiredWorkingDaysPerMonth is > 0
                ? (gross / employee.RequiredWorkingDaysPerMonth.Value) * workedUnits
                : gross;
            return (GrossSalary: gross, CalculatedSalary: calculated);
        }

        if (employee.Type == EmployeeType.Daily && employee.DailySalary.HasValue)
        {
            var gross = employee.DailySalary.Value;
            return (GrossSalary: gross, CalculatedSalary: gross * workedUnits);
        }

        // Sentinel: salary not configured
        return (GrossSalary: -1m, CalculatedSalary: -1m);
    }

    private async Task<(int CashboxId, int CashMovementTypeId)?> ResolveCashboxAndMovementTypeAsync(
        int? requestedCashboxId,
        int? requestedMovementTypeId,
        CancellationToken cancellationToken)
    {
        int cashboxId;
        if (requestedCashboxId.HasValue)
        {
            cashboxId = requestedCashboxId.Value;
        }
        else
        {
            var defaultCashbox = await dbContext.Cashboxes
                .AsNoTracking()
                .Where(c => c.CompanyId == companyId && c.IsActive)
                .OrderBy(c => c.Id)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (defaultCashbox == 0)
                return null;

            cashboxId = defaultCashbox;
        }

        int movementTypeId;
        if (requestedMovementTypeId.HasValue)
        {
            movementTypeId = requestedMovementTypeId.Value;
        }
        else
        {
            var defaultMovementType = await dbContext.CashMovementTypes
                .AsNoTracking()
                .Where(m => m.CompanyId == companyId && m.IsActive)
                .OrderBy(m => m.Id)
                .Select(m => m.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (defaultMovementType == 0)
                return null;

            movementTypeId = defaultMovementType;
        }

        return (cashboxId, movementTypeId);
    }
}
