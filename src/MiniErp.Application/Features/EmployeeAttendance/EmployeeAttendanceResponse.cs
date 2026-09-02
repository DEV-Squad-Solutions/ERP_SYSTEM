using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeAttendance;

public sealed record EmployeeAttendanceResponse(
    int Id,
    int CompanyId,
    int EmployeeId,
    string EmployeeName,
    EmployeeAttendanceStatus Status,
    DateOnly WorkDate,
    TimeOnly? CheckIn,
    TimeOnly? CheckOut,
    TimeOnly? WorkHours,
    WorkDayRatio WorkDayRatio,
    WorkDayRatio? WorkOverTimeRatio,
    WorkDayRatio? WorkDaysDeductionRatio,
    string? WorkLocation,
    string? Notes);

public sealed record EmployeeAttendanceReportRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    int? EmployeeId = null);

public sealed record EmployeeAttendanceReportLine(
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    int PresentDays,
    int AbsentDays,
    decimal WorkedUnits,
    decimal OvertimeUnits,
    decimal DeductionUnits);

public sealed record EmployeeAttendanceReportResponse(
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalEmployees,
    int TotalPresentDays,
    int TotalAbsentDays,
    decimal TotalWorkedUnits,
    decimal TotalOvertimeUnits,
    decimal TotalDeductionUnits,
    IReadOnlyList<EmployeeAttendanceReportLine> Employees);
