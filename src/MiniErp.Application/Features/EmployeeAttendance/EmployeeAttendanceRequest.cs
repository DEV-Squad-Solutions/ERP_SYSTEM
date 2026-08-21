using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeAttendance
{
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
    public sealed record EmployeeAttendanceUpdateRequest(
        int EmployeeId,
        AttendanceStatus? Status,
        DateOnly WorkDate,
        TimeOnly? CheckIn,
        TimeOnly? CheckOut,
        WorkDayRatio? WorkDayRatio,
        WorkDayRatio? WorkOverTimeRatio = null,
        WorkDayRatio? WorkDaysDeductionRatio = null,
        string? WorkLocation = null,
        string? Notes = null);

    public sealed record EmployeeAttendanceFilterRequest(
    int? EmployeeId = null,
    DateOnly? WorkDateFrom = null,
    DateOnly? WorkDateTo = null,
    AttendanceStatus? Status = null,
    string? Search = null);

    public sealed record BulkEmployeeAttendanceRequest(
        List<IndividualAttendanceRecordRequest> Attendances);

    public sealed record IndividualAttendanceRecordRequest(
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
}