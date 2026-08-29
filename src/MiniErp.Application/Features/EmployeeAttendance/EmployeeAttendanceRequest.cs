using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeAttendance
{
    public sealed record EmployeeAttendanceRequest(
        int EmployeeId,
        EmployeeAttendanceStatus Status,
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
        EmployeeAttendanceStatus? Status,
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
        EmployeeAttendanceStatus? Status = null,
        string? Search = null);

    public sealed record BulkEmployeeAttendanceRequest(
        List<IndividualAttendanceRecordRequest> Attendances);

    public sealed record IndividualAttendanceRecordRequest(
        int EmployeeId,
        EmployeeAttendanceStatus Status,
        DateOnly WorkDate,
        TimeOnly? CheckIn,
        TimeOnly? CheckOut,
        WorkDayRatio WorkDayRatio = WorkDayRatio.FullDay,
        WorkDayRatio? WorkOverTimeRatio = null,
        WorkDayRatio? WorkDaysDeductionRatio = null,
        string? WorkLocation = null,
        string? Notes = null);

    public sealed record BulkEmployeeAttendanceUpdateRequest(
        List<IndividualAttendanceRecordUpdateRequest> Attendances);

    public sealed record IndividualAttendanceRecordUpdateRequest(
        int Id,
        int EmployeeId,
        EmployeeAttendanceStatus? Status,
        DateOnly WorkDate,
        TimeOnly? CheckIn,
        TimeOnly? CheckOut,
        WorkDayRatio? WorkDayRatio,
        WorkDayRatio? WorkOverTimeRatio = null,
        WorkDayRatio? WorkDaysDeductionRatio = null,
        string? WorkLocation = null,
        string? Notes = null);

    public sealed record BulkEmployeeAttendanceDeleteRequest(
        List<int> AttendanceIds);
}