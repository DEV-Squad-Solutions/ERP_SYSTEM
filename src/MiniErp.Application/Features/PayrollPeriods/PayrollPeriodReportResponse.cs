using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollPeriods;

/// <summary>
/// Detailed salary report line for a single employee within a payroll period or date range.
/// </summary>
public sealed record PayrollPeriodEmployeeReportLine(
    int PayrollEntryId,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    EmployeeType EmployeeType,
    DateOnly StartDate,
    DateOnly EndDate,
    int PresentDays,
    int AbsentDays,
    decimal WorkedUnits,
    decimal? OvertimeUnits,
    decimal? DeductionUnits,
    decimal GrossSalary,
    decimal CalculatedSalary,
    decimal? Bonus,
    decimal? Deduction,
    decimal NetSalary,
    bool IsPaid);

/// <summary>
/// Aggregate summary row for an entire payroll period or date range.
/// </summary>
public sealed record PayrollPeriodReportSummary(
    int TotalEntries,
    int TotalEmployees,
    int MonthlyEmployeeCount,
    int DailyEmployeeCount,
    decimal TotalGrossSalary,
    decimal TotalCalculatedSalary,
    decimal TotalBonus,
    decimal TotalDeduction,
    decimal TotalNetSalary,
    decimal TotalPresentDays,
    decimal TotalAbsentDays,
    decimal TotalWorkedUnits,
    decimal TotalOvertimeUnits,
    decimal TotalDeductionUnits,
    int PaidCount,
    int PendingCount,
    decimal PaidAmount,
    decimal PendingAmount);

/// <summary>
/// Full payroll period report, optionally tied to a stored PayrollPeriod entity.
/// </summary>
public sealed record PayrollPeriodReportResponse(
    int? PeriodId,
    string? PeriodCode,
    string? PeriodName,
    DateOnly? PeriodStatus,
    DateOnly StartDate,
    DateOnly EndDate,
    PayrollPeriodReportSummary Summary,
    IReadOnlyList<PayrollPeriodEmployeeReportLine> Employees);
