using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeAttendance;

public sealed record EmployeeAttendanceRequest(
    int EmployeeId,
    AttendanceStatus Status,
    DateOnly WorkDate,
    TimeOnly? CheckIn,
    TimeOnly? CheckOut,
    WorkDayRatio WorkDayRatio = WorkDayRatio.FullDay,
    WorkDayRatio? WorkOverTimeRatio = null,
    WorkDayRatio? WorkDaysDeductionRatio = null,
    string? WorkLocation = null,
    string? Notes = null);
