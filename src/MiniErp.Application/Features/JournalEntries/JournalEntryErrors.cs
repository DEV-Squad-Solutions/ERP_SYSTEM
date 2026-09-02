using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.JournalEntries;

public static class JournalEntryErrors
{
    public static Error InvalidId() => Error.Validation(
        "JournalEntries.InvalidId",
        "يجب أن يكون رقم القيد أكبر من صفر.");

    public static Error NotFound(int id) => Error.NotFound(
        "JournalEntries.NotFound",
        $"لم يتم العثور على القيد رقم {id}.");

    public static Error FiscalYearNotFound(int id) => Error.Validation(
        "JournalEntries.FiscalYearNotFound",
        $"السنة المالية رقم {id} غير موجودة في الشركة الحالية.",
        nameof(JournalEntryRequest.FiscalYearId));

    public static Error EntryDateOutsideFiscalYear() => Error.Validation(
        "JournalEntries.EntryDateOutsideFiscalYear",
        "تاريخ القيد يجب أن يكون داخل نطاق السنة المالية المختارة.",
        nameof(JournalEntryRequest.EntryDate));

    public static Error FiscalYearClosed() => Error.Conflict(
        "JournalEntries.FiscalYearClosed",
        "لا يمكن إضافة أو تعديل أو حذف قيد داخل سنة مالية مغلقة.",
        nameof(JournalEntryRequest.FiscalYearId));

    public static Error AccountNotFound(int accountId, int lineIndex) =>
        Error.Validation(
            "JournalEntries.AccountNotFound",
            $"الحساب رقم {accountId} غير موجود في الشركة الحالية.",
            $"Lines[{lineIndex}].AccountId");

    public static Error AccountInactive(int accountId, int lineIndex) =>
        Error.Validation(
            "JournalEntries.AccountInactive",
            $"الحساب رقم {accountId} غير فعال.",
            $"Lines[{lineIndex}].AccountId");

    public static Error AccountNotPosting(int accountId, int lineIndex) =>
        Error.Validation(
            "JournalEntries.AccountNotPosting",
            $"الحساب رقم {accountId} حساب رئيسي ولا يسمح بالتسجيل عليه.",
            $"Lines[{lineIndex}].AccountId");

    public static Error AccountMustBeChild(int accountId, int lineIndex) =>
        Error.Validation(
            "JournalEntries.AccountMustBeChild",
            $"الحساب رقم {accountId} حساب رئيسي. القيود اليومية تُسجل على الحسابات الفرعية فقط.",
            $"Lines[{lineIndex}].AccountId");

    public static Error AccountLinkedToOperationalData(
        int accountId,
        int lineIndex) => Error.Validation(
            "JournalEntries.AccountLinkedToOperationalData",
            $"الحساب رقم {accountId} مرتبط بعنصر تشغيلي ولا يمكن استخدامه في قيد يدوي أو تسوية.",
            $"Lines[{lineIndex}].AccountId");

    public static Error Unbalanced() => Error.Validation(
        "JournalEntries.Unbalanced",
        "إجمالي المدين يجب أن يساوي إجمالي الدائن ويكون أكبر من صفر.",
        nameof(JournalEntryRequest.Lines));

    public static Error RowVersionRequired() => Error.Validation(
        "JournalEntries.RowVersionRequired",
        "يجب إرسال إصدار القيد الحالي قبل تعديله أو حذفه.",
        nameof(JournalEntryUpdateRequest.RowVersion));

    public static Error Concurrency() => Error.Conflict(
        "JournalEntries.Concurrency",
        "تغير القيد بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");

    public static Error AutomaticReadOnly() => Error.Conflict(
        "JournalEntries.AutomaticReadOnly",
        "لا يمكن تعديل أو حذف القيد التلقائي مباشرة. عدّل الحركة المصدر أولًا.");

    public static Error AutomaticCannotBeCreatedManually() => Error.Validation(
        "JournalEntries.AutomaticCannotBeCreatedManually",
        "القيد التلقائي يتم إنشاؤه من الحركة المصدر فقط.",
        nameof(JournalEntryRequest.EntryType));

    public static Error AutomaticSourceRequired() => Error.Validation(
        "JournalEntries.AutomaticSourceRequired",
        "يجب تحديد مصدر القيد التلقائي ورقم الحركة.");

    public static Error AutomaticDuplicate() => Error.Conflict(
        "JournalEntries.AutomaticDuplicate",
        "يوجد قيد تلقائي فعال لنفس الحركة بالفعل.");

    public static Error ReversedReadOnly() => Error.Conflict(
        "JournalEntries.ReversedReadOnly",
        "لا يمكن تعديل أو حذف قيد تم عكسه.");
}
