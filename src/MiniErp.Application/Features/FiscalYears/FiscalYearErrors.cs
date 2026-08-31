using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.FiscalYears;

public static class FiscalYearErrors
{
    public static Error InvalidId() =>
        Error.Validation(
            "FiscalYears.InvalidId",
            "يجب أن يكون رقم السنة المالية أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "FiscalYears.NotFound",
            $"لم يتم العثور على السنة المالية رقم {id}.");

    public static Error CurrentNotFound() =>
        Error.NotFound(
            "FiscalYears.CurrentNotFound",
            "لا توجد سنة مالية حالية للشركة.");

    public static Error NameExists(string name) =>
        Error.Conflict(
            "FiscalYears.NameExists",
            $"اسم السنة المالية '{name}' مستخدم بالفعل في نفس الشركة.",
            nameof(FiscalYearRequest.Name));

    public static Error DateRangeInvalid() =>
        Error.Validation(
            "FiscalYears.DateRangeInvalid",
            "يجب أن يكون تاريخ بداية السنة المالية قبل تاريخ نهايتها.");

    public static Error DateRangeOverlaps() =>
        Error.Conflict(
            "FiscalYears.DateRangeOverlaps",
            "الفترة المحددة تتداخل مع سنة مالية أخرى في نفس الشركة.");

    public static Error RowVersionRequired() =>
        Error.Validation(
            "FiscalYears.RowVersionRequired",
            "يجب إرسال إصدار السنة المالية الحالي للتعديل.",
            nameof(FiscalYearUpdateRequest.RowVersion));

    public static Error Concurrency() =>
        Error.Conflict(
            "FiscalYears.Concurrency",
            "تم تعديل السنة المالية بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");

    public static Error ClosedCannotBeModified() =>
        Error.Conflict(
            "FiscalYears.ClosedCannotBeModified",
            "لا يمكن تعديل سنة مالية مغلقة. أعد فتحها أولًا.");

    public static Error AlreadyClosed() =>
        Error.Conflict(
            "FiscalYears.AlreadyClosed",
            "السنة المالية مغلقة بالفعل.");

    public static Error AlreadyOpen() =>
        Error.Conflict(
            "FiscalYears.AlreadyOpen",
            "السنة المالية مفتوحة بالفعل.");

    public static Error CurrentCannotBeDeleted() =>
        Error.Conflict(
            "FiscalYears.CurrentCannotBeDeleted",
            "لا يمكن حذف السنة المالية الحالية. اجعل سنة أخرى هي الحالية أولًا.");

    public static Error ClosedCannotBeDeleted() =>
        Error.Conflict(
            "FiscalYears.ClosedCannotBeDeleted",
            "لا يمكن حذف سنة مالية مغلقة.");
}
