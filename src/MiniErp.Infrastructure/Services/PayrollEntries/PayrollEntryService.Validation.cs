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
        if (!string.IsNullOrWhiteSpace(filters.Search) && filters.Search.Length > 100)
            return Error.Validation(
                "PayrollEntry.SearchTooLong",
                "عبارة البحث طويلة جدًا."
                , nameof(filters.Search));
        if (filters.EmployeeId != null && filters.EmployeeId <= 0)
            return Error.Validation(
                "PayrollEntry.InvalidEmployeeId",
                "معرف الموظف غير صالح."
                , nameof(filters.EmployeeId));
        if (filters.StartDate != null && filters.EndDate != null && filters.StartDate > filters.EndDate)
            return Error.Validation(
                "PayrollEntry.InvalidDateRange",
                "تاريخ البدء لا يمكن أن يكون بعد تاريخ الانتهاء."
                , nameof(filters.StartDate));
        if (filters.EmployeeType != null && !Enum.IsDefined(typeof(EmployeeType), filters.EmployeeType))
            return Error.Validation(
                "PayrollEntry.InvalidEmployeeType",
                "نوع الموظف المحدد غير صالح."
                , nameof(filters.EmployeeType));

        return null;
    }
    private static Error? ValidateAddAsync(PayrollEntryCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.EmployeeId <= 0)
            return Error.Validation(
                "PayrollEntry.InvalidEmployeeId",
                "معرف الموظف غير صالح.",
                nameof(request.EmployeeId));

        if (request.Bonus.HasValue && request.Bonus.Value < 0)
            return Error.Validation(
                "PayrollEntry.InvalidBonus",
                "المكافأة يجب أن تكون أكبر من أو تساوي صفر.",
                nameof(request.Bonus));

        if (request.Deduction.HasValue && request.Deduction.Value < 0)
            return Error.Validation(
                "PayrollEntry.InvalidDeduction",
                "الخصم يجب أن يكون أكبر من أو تساوي صفر.",
                nameof(request.Deduction));

        return null;
    }

    private static Error? ValidateAddBulkAsync(BulkPayrollEntryCreateRequest request)
    {
        if (request.Entries is null || request.Entries.Count == 0)
            return Error.Validation(
                "PayrollEntry.EmptyBulkRequest",
                "يجب إرسال مدخل راتب واحد على الأقل.");

        if (request.DefaultStartDate.HasValue && request.DefaultEndDate.HasValue && request.DefaultStartDate.Value > request.DefaultEndDate.Value)
            return Error.Validation(
                "PayrollEntry.InvalidDateRange",
                "تاريخ البدء الافتراضي لا يمكن أن يكون بعد تاريخ الانتهاء الافتراضي.",
                nameof(request.DefaultStartDate));

        var duplicateEmployees = request.Entries
            .GroupBy(e => e.EmployeeId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateEmployees.Count > 0)
            return Error.Validation(
                "PayrollEntry.DuplicateEmployee",
                $"لا يجوز تكرار نفس الموظف داخل الطلب الواحد: {string.Join(", ", duplicateEmployees)}");

        return null;
    }

    private static Error? ValidateBulkPaymentAsync(BulkPayrollEntrySalaryPaymentRequest request)
    {
        var hasEntries = request.Entries is { Count: > 0 };
        var hasIds = request.PayrollEntryIds is { Count: > 0 };

        if (!hasEntries && !hasIds)
            return Error.Validation(
                "PayrollEntry.EmptyBulkPaymentRequest",
                "يجب تحديد قيود الرواتب المطلوب صرفها.");

        if (hasEntries)
        {
            var duplicateIds = request.Entries!
                .GroupBy(e => e.PayrollEntryId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Count > 0)
                return Error.Validation(
                    "PayrollEntry.DuplicateEntryId",
                    $"لا يجوز تكرار قيد الراتب داخل الطلب الواحد: {string.Join(", ", duplicateIds)}");
        }

        if (hasIds)
        {
            var duplicateIds = request.PayrollEntryIds!
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Count > 0)
                return Error.Validation(
                    "PayrollEntry.DuplicateEntryId",
                    $"لا يجوز تكرار قيد الراتب داخل الطلب الواحد: {string.Join(", ", duplicateIds)}");
        }

        return null;
    }

    private static Error? ValidateForPayment(
        Domain.Entities.Payroll.PayrollEntry entry)
    {
        if (entry.IsSalaryMoveToEmployeeAccount)
            return Error.Conflict(
                "PayrollEntry.AlreadyPaid",
                $"تم تحويل راتب القيد رقم {entry.Id} إلى حساب الموظف مسبقًا.");

        // Negative net salary indicates a data error (deductions exceed gross).
        // Zero net salary is a valid business outcome (e.g. full-month absence)
        // — the entry is still "paid" to record that the period was processed.
        if (entry.NetSalary < 0)
            return Error.Validation(
                "PayrollEntry.NegativeSalary",
                $"صافي راتب القيد رقم {entry.Id} سالب. يرجى مراجعة الخصومات.");

        return null;
    }

    /// <summary>
    /// Guards UpdateAsync / RecalculateAsync — entry must not have been paid yet.
    /// </summary>
    private static Error? ValidateForUpdate(Domain.Entities.Payroll.PayrollEntry entry)
    {
        if (entry.IsSalaryMoveToEmployeeAccount)
            return Error.Conflict(
                "PayrollEntry.AlreadyPaid",
                $"لا يمكن تعديل قيد الراتب رقم {entry.Id} لأن راتبه قد تم تحويله إلى حساب الموظف.");

        return null;
    }
}
