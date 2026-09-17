using MiniErp.Application.Common.Results;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PayrollEntries;

public readonly record struct PayrollPeriodRecord(
    int Id,
    int EmployeeId,
    DateOnly StartDate,
    DateOnly EndDate);

public static class PayrollEntryBusinessRules
{
    /// <summary>
    /// Enforces the financial lock rule: a payroll entry can only be edited or deleted
    /// before its salary is moved to the employee's account.
    /// Returns Conflict error if the entry is locked.
    /// </summary>
    public static Error? EnsureEditable(this PayrollEntry entry)
    {
        if (!entry.CanBeEdited())
        {
            return Error.Conflict(
                "PayrollEntry.AlreadyPaid",
                $"لا يمكن تعديل قيد الراتب رقم {entry.Id} لأن راتبه قد تم تحويله إلى حساب الموظف.");
        }

        return null;
    }

    /// <summary>
    /// Authoritatively determines the StartDate for an employee's payroll period.
    /// StartDate = Employee.LastDayOfReceivingSalary + 1 day.
    /// If LastDayOfReceivingSalary is null, falls back to employee creation date
    /// (or the 1st of the month of defaultEndDate).
    /// </summary>
    public static DateOnly GetEffectiveStartDate(this Employee employee, DateOnly defaultEndDate)
    {
        if (employee.LastDayOfReceivingSalary.HasValue)
        {
            return employee.LastDayOfReceivingSalary.Value.AddDays(1);
        }

        if (employee.CreatedOn != default)
        {
            var createdDate = DateOnly.FromDateTime(employee.CreatedOn);
            if (createdDate <= defaultEndDate)
            {
                return createdDate;
            }
        }

        return new DateOnly(defaultEndDate.Year, defaultEndDate.Month, 1);
    }

    /// <summary>
    /// Calculates total inclusive calendar days between StartDate and EndDate:
    /// TotalDays = (EndDate - StartDate).Days + 1
    /// </summary>
    public static int CalculateTotalDays(DateOnly startDate, DateOnly endDate) =>
        (endDate.DayNumber - startDate.DayNumber) + 1;

    /// <summary>
    /// Calculates AbsentDays based on TotalDays and PresentDays:
    /// AbsentDays = TotalDays - PresentDays
    /// </summary>
    public static int CalculateAbsentDays(int totalDays, int presentDays) =>
        totalDays - presentDays;

    /// <summary>
    /// Validates that PresentDays is non-negative and does not exceed TotalDays in the period.
    /// </summary>
    public static Error? ValidatePresentDays(int presentDays, int totalDays)
    {
        if (presentDays < 0 || presentDays > totalDays)
        {
            return Error.Validation(
                "PayrollEntry.InvalidPresentDays",
                $"عدد أيام الحضور ({presentDays}) غير صالح. يجب أن يكون بين 0 وإجمالي عدد الأيام في الفترة ({totalDays}).");
        }

        return null;
    }

    /// <summary>
    /// Validates employee active status and OutCompany workplace status.
    /// </summary>
    public static Error? ValidateOutCompanyEligibility(Employee employee)
    {
        if (!employee.IsActive)
        {
            return Error.Validation(
                "Employee.Inactive",
                "لا يمكن إنشاء مسير رواتب لموظف غير نشط.");
        }

        if (employee.WorkPlaceStatus != WorkPlaceStatus.OutCompany)
        {
            return Error.Validation(
                "PayrollEntry.NotOutCompany",
                "مسير رواتب خارج الشركة مخصص فقط للموظفين الذين يعملون خارج الشركة.");
        }

        return null;
    }

    /// <summary>
    /// Validates OutCompany date range: StartDate and EndDate required, StartDate <= EndDate.
    /// Note: Does NOT restrict StartDate based on Employee.CreatedOn or HireDate.
    /// </summary>
    public static Error? ValidateOutCompanyDates(DateOnly startDate, DateOnly endDate, string? employeeName = null)
    {
        var prefix = string.IsNullOrWhiteSpace(employeeName) ? string.Empty : $" للموظف {employeeName}";

        if (startDate == default)
        {
            return Error.Validation("PayrollEntry.StartDateRequired", $"تاريخ البداية{prefix} مطلوب.");
        }

        if (endDate == default)
        {
            return Error.Validation("PayrollEntry.EndDateRequired", $"تاريخ النهاية{prefix} مطلوب.");
        }

        if (startDate > endDate)
        {
            return Error.Validation(
                "PayrollEntry.InvalidDateRange",
                $"تاريخ البداية{prefix} ({startDate:yyyy-MM-dd}) يجب أن يكون قبل أو يساوي تاريخ النهاية ({endDate:yyyy-MM-dd}).");
        }

        return null;
    }

    /// <summary>
    /// Validates OutCompany payroll period against existing payroll entries for this employee:
    /// 1. Excludes currentEntryId if updating.
    /// 2. Validates that no existing payroll entry overlaps with the requested period.
    /// 3. If a previous applicable payroll exists, New StartDate must satisfy: StartDate > Previous.EndDate.
    /// 4. If no previous payroll exists, allows StartDate before Employee creation/hiring date.
    /// </summary>
    public static Error? ValidateOutCompanyPayrollPeriod(
        DateOnly startDate,
        DateOnly endDate,
        IEnumerable<PayrollPeriodRecord> existingEntries,
        int? currentEntryId = null,
        string? employeeName = null)
    {
        var applicableEntries = existingEntries
            .Where(e => !currentEntryId.HasValue || e.Id != currentEntryId.Value)
            .ToList();

        if (applicableEntries.Count == 0)
        {
            return null;
        }

        // 1. Check for overlapping periods
        var overlap = applicableEntries.FirstOrDefault(e => e.StartDate <= endDate && e.EndDate >= startDate);
        if (overlap != default)
        {
            var empMsg = string.IsNullOrWhiteSpace(employeeName) ? string.Empty : $" للموظف {employeeName}";
            return Error.Conflict(
                "PayrollEntry.PeriodOverlap",
                $"يوجد مسير رواتب مسجل مسبقًا{empMsg} يغطي أو يتداخل مع هذه الفترة ({startDate:yyyy-MM-dd} إلى {endDate:yyyy-MM-dd}).");
        }

        // 2. Check previous payroll rule:
        // Find most recent previous applicable OutCompany entry based on actual payroll dates (EndDate descending).
        var previousPayroll = applicableEntries
            .Where(e => e.EndDate < endDate)
            .OrderByDescending(e => e.EndDate)
            .ThenByDescending(e => e.StartDate)
            .FirstOrDefault();

        if (previousPayroll != default && startDate <= previousPayroll.EndDate)
        {
            var empMsg = string.IsNullOrWhiteSpace(employeeName) ? string.Empty : $" للموظف {employeeName}";
            return Error.Conflict(
                "PayrollEntry.PeriodOverlap",
                $"تاريخ البداية ({startDate:yyyy-MM-dd}){empMsg} يجب أن يكون بعد تاريخ نهاية آخر مسير رواتب ({previousPayroll.EndDate:yyyy-MM-dd}).");
        }

        return null;
    }
}
