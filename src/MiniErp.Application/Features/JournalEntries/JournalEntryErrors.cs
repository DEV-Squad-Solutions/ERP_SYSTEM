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
        "لا يمكن تسجيل أو عكس قيد داخل سنة مالية مغلقة.",
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

    public static Error Unbalanced() => Error.Validation(
        "JournalEntries.Unbalanced",
        "إجمالي المدين يجب أن يساوي إجمالي الدائن ويكون أكبر من صفر.",
        nameof(JournalEntryRequest.Lines));

    public static Error AlreadyReversed() => Error.Conflict(
        "JournalEntries.AlreadyReversed",
        "تم عكس هذا القيد بالفعل.");

    public static Error CannotReverseReversal() => Error.Conflict(
        "JournalEntries.CannotReverseReversal",
        "لا يمكن عكس قيد عكسي. أنشئ قيدًا جديدًا إذا لزم التصحيح.");

    public static Error ReversalDateBeforeEntry() => Error.Validation(
        "JournalEntries.ReversalDateBeforeEntry",
        "تاريخ العكس لا يمكن أن يسبق تاريخ القيد الأصلي.",
        nameof(JournalEntryReverseRequest.ReversalDate));

    public static Error RowVersionRequired() => Error.Validation(
        "JournalEntries.RowVersionRequired",
        "يجب إرسال إصدار القيد الحالي قبل عكسه.",
        nameof(JournalEntryReverseRequest.RowVersion));

    public static Error Concurrency() => Error.Conflict(
        "JournalEntries.Concurrency",
        "تغير القيد بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");
}
