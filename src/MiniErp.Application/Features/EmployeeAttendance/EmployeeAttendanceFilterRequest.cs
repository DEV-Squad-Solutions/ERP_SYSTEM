using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeAttendance;

public sealed record EmployeeAttendanceFilterRequest(
    int? EmployeeId = null,
    DateOnly? WorkDateFrom = null,
    DateOnly? WorkDateTo = null,
    AttendanceStatus? Status = null,
    string? Search = null);
