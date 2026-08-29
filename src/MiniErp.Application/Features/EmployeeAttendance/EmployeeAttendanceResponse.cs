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
