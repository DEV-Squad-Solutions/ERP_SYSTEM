using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.PayrollReport;
using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.PayrollReports;

public sealed class PayrollReportService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IPayrollReportService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;


    // ─── HELPER: BUILD REPORT ───────────────────────────────────────────────

    public async Task<Result<PayrollReportResponse>> BuildReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        int? employeeId = null,
        bool? isMoved = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.PayrollEntries
            .AsNoTracking()
            .Where(e =>
                e.CompanyId == companyId &&
                e.StartDate >= startDate &&
                e.EndDate <= endDate);

        if (employeeId.HasValue)
        {
            query = query.Where(e => e.EmployeeId == employeeId.Value);
        }

        if (isMoved.HasValue)
        {
            query = query.Where(e => e.IsSalaryMoveToEmployeeAccount == isMoved.Value);
        }

        var entries = await query
            .OrderBy(e => e.EmployeeName)
            .Select(e => new
            {
                e.Id,
                e.EmployeeId,
                e.EmployeeCode,
                e.EmployeeName,
                e.EmployeeType,
                e.StartDate,
                e.EndDate,
                e.PresentDays,
                e.AbsentDays,
                WorkedUnits = e.WorkedDaysbydayunit,
                OvertimeUnits = e.Overtimebydayunit,
                DeductionUnits = e.Deductionbydayunit,
                e.GrossSalary,
                e.CalculatedSalary,
                e.Bonus,
                e.Deduction,
                e.NetSalary,
                IsPaid = e.IsSalaryMoveToEmployeeAccount
            })
            .ToListAsync(cancellationToken);

        var employees = entries
            .Select(e => new PayrollEmployeeReportLine(
                PayrollEntryId: e.Id,
                EmployeeId: e.EmployeeId,
                EmployeeCode: e.EmployeeCode,
                EmployeeName: e.EmployeeName,
                EmployeeType: e.EmployeeType,
                StartDate: e.StartDate,
                EndDate: e.EndDate,
                PresentDays: e.PresentDays,
                AbsentDays: e.AbsentDays,
                WorkedUnits: e.WorkedUnits,
                OvertimeUnits: e.OvertimeUnits,
                DeductionUnits: e.DeductionUnits,
                GrossSalary: e.GrossSalary,
                CalculatedSalary: e.CalculatedSalary,
                Bonus: e.Bonus,
                Deduction: e.Deduction,
                NetSalary: e.NetSalary,
                IsPaid: e.IsPaid))
            .ToList();

        var summary = new PayrollReportSummary(
            TotalEntries: entries.Count,

            TotalEmployees: entries
                .Select(e => e.EmployeeId)
                .Distinct()
                .Count(),

            MonthlyEmployeeCount: entries
                .Count(e => e.EmployeeType == EmployeeType.Monthly),

            DailyEmployeeCount: entries
                .Count(e => e.EmployeeType == EmployeeType.Daily),

            TotalGrossSalary: entries.Sum(e => e.GrossSalary),

            TotalCalculatedSalary: entries.Sum(e => e.CalculatedSalary),

            TotalBonus: entries.Sum(e => e.Bonus ?? 0m),

            TotalDeduction: entries.Sum(e => e.Deduction ?? 0m),

            TotalNetSalary: entries.Sum(e => e.NetSalary),

            TotalPresentDays: entries.Sum(e => (decimal)e.PresentDays),

            TotalAbsentDays: entries.Sum(e => (decimal)e.AbsentDays),

            TotalWorkedUnits: entries.Sum(e => e.WorkedUnits),

            TotalOvertimeUnits: entries.Sum(e => e.OvertimeUnits ?? 0m),

            TotalDeductionUnits: entries.Sum(e => e.DeductionUnits ?? 0m),

            PaidCount: entries.Count(e => e.IsPaid),

            PendingCount: entries.Count(e => !e.IsPaid),

            PaidAmount: entries
                .Where(e => e.IsPaid)
                .Sum(e => e.NetSalary),

            PendingAmount: entries
                .Where(e => !e.IsPaid)
                .Sum(e => e.NetSalary));

        return Result<PayrollReportResponse>.Success(
            new PayrollReportResponse(
                StartDate: startDate,
                EndDate: endDate,
                Summary: summary,
                Employees: employees));
    }
}
