using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollReport;

public sealed record PayrollEmployeeReportLine(
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

public sealed record PayrollReportSummary(
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

public sealed record PayrollReportResponse(
    DateOnly StartDate,
    DateOnly EndDate,
    PayrollReportSummary Summary,
    IReadOnlyList<PayrollEmployeeReportLine> Employees);
