using System;
using System.Collections.Generic;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeAttendance;

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
