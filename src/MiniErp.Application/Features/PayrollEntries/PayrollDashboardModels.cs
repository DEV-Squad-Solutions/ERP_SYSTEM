using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollEntries;

public sealed record PayrollDashboardFilterRequest(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int? EmployeeId = null,
    EmployeeType? EmployeeType = null);

public sealed record PayrollDashboardPendingEntryResponse(
    int Id,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    EmployeeType EmployeeType,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal GrossSalary,
    decimal NetSalary,
    decimal? Bonus,
    decimal? Deduction,
    bool IsSalaryMoveToEmployeeAccount);

public sealed record PayrollDashboardRecentOperationResponse(
    int SourceId,
    string OperationType,
    string OperationName,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    DateOnly Date,
    decimal Amount,
    CurrencyCode Currency,
    string? ReferenceNumber,
    string? Notes);

public sealed record PayrollDashboardResponse(
    decimal TotalPayrolls,
    decimal NetPayable,
    decimal TotalPaid,
    decimal TotalDeductions,
    decimal TotalAdvances,
    int EmployeeCount,
    IReadOnlyList<PayrollDashboardPendingEntryResponse> PendingPayrolls,
    IReadOnlyList<PayrollDashboardRecentOperationResponse> RecentOperations);
