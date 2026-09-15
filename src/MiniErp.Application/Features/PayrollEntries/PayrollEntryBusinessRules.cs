using MiniErp.Application.Common.Results;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Payroll;

namespace MiniErp.Application.Features.PayrollEntries;

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
}
