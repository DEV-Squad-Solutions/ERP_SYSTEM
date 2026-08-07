using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollEntries;

public sealed record PayrollEntryResponse(
    int Id,
    int CompanyId,
    DateOnly StartDate,
    DateOnly EndDate,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    EmployeeType EmployeeType,
    decimal Bonus,
    decimal Deduction,
    decimal? GrossSalary,
    decimal? NetSalary, 
    AttendanceSummary AttendanceSummary);
public record class AttendanceSummary(
    int PresentDays,
    int AbsentDays,
    decimal TotalPresentDays,
    decimal? TotalOvertimeDays,
    decimal? TotalDeductionDays);
