using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.EmployeeAttendance;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.PayrollEntries;

public sealed partial class PayrollEntryService
{
    /// <summary>
    /// Guards the PaySalaryAsync operation before any DB work begins.
    /// Returns an Error if the entry is already paid or has no salary to disburse.
    /// </summary>

    private static Error? ValidateFilters(PayrollEntryFilterRequest filters, CancellationToken cancellationToken)
    {
        if(!string.IsNullOrWhiteSpace(filters.Search) && filters.Search.Length > 100)
            return Error.Validation(
                "PayrollEntry.SearchTooLong",
                "عبارة البحث طويلة جدًا."
                , nameof(filters.Search));
        if (filters.EmployeeId != null && filters.EmployeeId <= 0)
            return Error.Validation(
                "PayrollEntry.InvalidEmployeeId",
                "معرف الموظف غير صالح."
                , nameof(filters.EmployeeId));
        if(filters.StartDate != null && filters.EndDate != null && filters.StartDate > filters.EndDate)
            return Error.Validation(
                "PayrollEntry.InvalidDateRange",
                "تاريخ البدء لا يمكن أن يكون بعد تاريخ الانتهاء."
                , nameof(filters.StartDate));
        if(filters.EmployeeType != null && !Enum.IsDefined(typeof(EmployeeType), filters.EmployeeType))
            return Error.Validation(
                "PayrollEntry.InvalidEmployeeType",
                "نوع الموظف المحدد غير صالح."
                , nameof(filters.EmployeeType));

        return null;
    }
    private static Error? ValidateAddAsync(PayrollEntryCreateRequest filters, CancellationToken cancellationToken)
    {
        if (filters.EmployeeId <= 0)
            return Error.Validation(
                "PayrollEntry.InvalidEmployeeId",
                "معرف الموظف غير صالح."
                , nameof(filters.EmployeeId));
        if(filters.CashboxId <= 0)
            return Error.Validation(
                "PayrollEntry.InvalidCashboxId",
                "معرف الخزينه غير صالح."
                , nameof(filters.CashboxId));
        if(filters.CashMovementTypeId <= 0)
            return Error.Validation(
                "PayrollEntry.InvalidCashMovementTypeId",
                "معرف نوع حركة النقدية غير صالح."
                , nameof(filters.CashMovementTypeId));
        if (filters.StartDate != null &&filters.EndDate != null && filters.StartDate > filters.EndDate)
            return Error.Validation(
                "PayrollEntry.InvalidDateRange",
                "تاريخ البدء لا يمكن أن يكون بعد تاريخ الانتهاء."
                , nameof(filters.StartDate));
       if(filters.Bonus < 0)
            return Error.Validation(
                "PayrollEntry.NegativeBonus",
                "لا يمكن أن يكون المكافأة سالبة."
                , nameof(filters.Bonus));
       if(filters.Deduction < 0)
            return Error.Validation(
                "PayrollEntry.NegativeDeduction",
                "لا يمكن أن يكون الخصم سالبًا."
                , nameof(filters.Deduction));
        return null;
    }
    private static Error? ValidateForPayment(
        Domain.Entities.Payroll.PayrollEntry entry)
    {
        if (entry.IsTakeSalary) //take salary means the salary has moved to employee account, so we cannot pay it again
            return Error.Conflict(
                "PayrollEntry.AlreadyPaid",
                "تم صرف راتب هذا القيد مسبقًا.");

        var amount = entry.NetSalary ?? entry.CalculatedSalary;
        if (amount <= 0)
            return Error.Validation(
                "PayrollEntry.NoSalaryAmount",
                "لا توجد قيمة صافي راتب لهذا القيد.");

        return null;
    }
}
