using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.EmployeeAttendance;
using MiniErp.Application.Features.Employees;
using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Infrastructure.Services.EmployeeAttendance
{
    public  sealed partial class EmployeeAttendanceService
    {
        private static Error? ValidateFilters(EmployeeAttendanceFilterRequest filters, CancellationToken cancellationToken)
        {

            if (!string.IsNullOrWhiteSpace(filters.Search) && filters.Search.Length > 100)
                return Error.Validation(
                    "EmployeeAttendance.SearchTooLong",
                    "عبارة البحث طويلة جدًا."
                    , nameof(filters.Search)); if (filters.EmployeeId != null && filters.EmployeeId <= 0)
                return Error.Validation(
                    "EmployeeAttendance.InvalidEmployeeId",
                    "معرف الموظف غير صالح."
                    , nameof(filters.EmployeeId));
            if(filters.WorkDateFrom != null && filters.WorkDateTo != null && filters.WorkDateFrom > filters.WorkDateTo)
                return Error.Validation(
                    "EmployeeAttendance.InvalidWorkDateRange",
                    "تاريخ العمل من يجب أن يكون قبل تاريخ العمل إلى."
                    , nameof(filters.WorkDateFrom));
            if (filters.Status is not null && !Enum.IsDefined(typeof(AttendanceStatus), filters.Status))
                return Error.Validation(
                    "EmployeeAttendance.InvalidStatus",
                    "حالة الحضور المحددة غير صالحة."
                    , nameof(filters.Status));
            return null;
        }
        private static Error? ValidateAddAsync(EmployeeAttendanceRequest filters, CancellationToken cancellationToken)
        {
            if (filters.EmployeeId <= 0)
                return Error.Validation(
                    "EmployeeAttendance.InvalidEmployeeId",
                    "معرف الموظف غير صالح."
                    , nameof(filters.EmployeeId));
            if (filters.WorkDate == default)
                return Error.Validation(
                    "EmployeeAttendance.InvalidWorkDate",
                    "تاريخ العمل غير صالح."
                    , nameof(filters.WorkDate));
            if (!Enum.IsDefined(typeof(AttendanceStatus), filters.Status))
                return Error.Validation(
                    "EmployeeAttendance.InvalidStatus",
                    "حالة الحضور المحددة غير صالحة."
                    , nameof(filters.Status));
            if (filters.Status == AttendanceStatus.Present)
            {
                if (filters.CheckIn == null)
                    return Error.Validation(
                        "EmployeeAttendance.MissingCheckIn",
                        "وقت تسجيل الدخول مفقود."
                        , nameof(filters.CheckIn));
            }
            if (filters.CheckOut != null && filters.CheckIn != null && filters.CheckOut < filters.CheckIn)
                return Error.Validation(
                    "EmployeeAttendance.InvalidCheckOutTime",
                    "وقت تسجيل الخروج يجب أن يكون بعد وقت تسجيل الدخول."
                    , nameof(filters.CheckOut));
            return null;
        }
        private static Error? ValidateUpdateAsync(EmployeeAttendanceUpdateRequest filters, CancellationToken cancellationToken)
        {
            if (filters.EmployeeId <= 0)
                return Error.Validation(
                    "EmployeeAttendance.InvalidEmployeeId",
                    "معرف الموظف غير صالح."
                    , nameof(filters.EmployeeId));
            if (filters.WorkDate == default)
                return Error.Validation(
                    "EmployeeAttendance.InvalidWorkDate",
                    "تاريخ العمل غير صالح."
                    , nameof(filters.WorkDate));
            if (filters.Status != null&& !Enum.IsDefined(typeof(AttendanceStatus), filters.Status))
                return Error.Validation(
                    "EmployeeAttendance.InvalidStatus",
                    "حالة الحضور المحددة غير صالحة."
                    , nameof(filters.Status));
            if (filters.Status == AttendanceStatus.Present)
            {
                if (filters.CheckIn == null)
                    return Error.Validation(
                        "EmployeeAttendance.MissingCheckIn",
                        "وقت تسجيل الدخول مفقود."
                        , nameof(filters.CheckIn));
                if (filters.CheckOut == null)
                    return Error.Validation(
                        "EmployeeAttendance.MissingCheckOut",
                        "وقت تسجيل الخروج مفقود."
                        , nameof(filters.CheckOut));
            }
            if(filters.CheckOut != null && filters.CheckIn != null && filters.CheckOut < filters.CheckIn)
                return Error.Validation(
                    "EmployeeAttendance.InvalidCheckOutTime",
                    "وقت تسجيل الخروج يجب أن يكون بعد وقت تسجيل الدخول."
                    , nameof(filters.CheckOut));

            return null;
        }


    }
}