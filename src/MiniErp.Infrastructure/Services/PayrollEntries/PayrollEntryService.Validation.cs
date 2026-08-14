using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Application.Common.Results;

namespace MiniErp.Infrastructure.Services.PayrollEntries;

public sealed partial class PayrollEntryService
{
    /// <summary>
    /// Guards the PaySalaryAsync operation before any DB work begins.
    /// Returns an Error if the entry is already paid or has no salary to disburse.
    /// </summary>
    private static Error? ValidateForPayment(
        Domain.Entities.Payroll.PayrollEntry entry)
    {
        if (entry.IsTakeSalary)
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
