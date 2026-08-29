using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollEntries;

public sealed record PayrollEntryPageResponse(
    IReadOnlyCollection<PayrollEntriesListResponse> PayrollEntries,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    AttendanceSummary AttendanceSummary);

public record class AttendanceSummary(
    int PresentDays,
    int AbsentDays,
    decimal TotalPresentDays,
    decimal? TotalOvertimeDays,
    decimal? TotalDeductionDays);

public record PayrollEntriesListResponse(
    int Id,
    int CompanyId,
    DateOnly StartDate,
    DateOnly EndDate,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    EmployeeType EmployeeType,
    decimal? Bonus,
    decimal? Deduction,
    decimal GrossSalary,
    decimal NetSalary,
    bool IsSalaryMoveToEmployeeAccount,
    DateOnly? SalaryMovedOn);

public record PayrollEntryResponse(
    int Id,
    int CompanyId,
    DateOnly StartDate,
    DateOnly EndDate,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    EmployeeType EmployeeType,
    decimal? Bonus,
    decimal? Deduction,
    decimal GrossSalary,
    decimal NetSalary,
    bool IsSalaryMoveToEmployeeAccount,
    DateOnly? SalaryMovedOn,
    AttendanceSummary AttendanceSummary);
