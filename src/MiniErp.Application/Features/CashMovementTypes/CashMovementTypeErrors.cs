using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.CashMovementTypes;

public static class CashMovementTypeErrors
{
    public static Error NameExists(string name) =>
        Error.Conflict(
            "CashMovementTypes.NameExists",
            $"نوع الحركة النقدية '{name}' موجود بالفعل في نفس الاتجاه.",
            nameof(CashMovementTypeRequest.Name));

    public static Error InvalidId() =>
        Error.Validation(
            "CashMovementTypes.InvalidId",
            "يجب أن يكون رقم نوع الحركة النقدية أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "CashMovementTypes.NotFound",
            $"لم يتم العثور على نوع الحركة النقدية رقم {id}.");

    public static Error RowVersionRequired() =>
        Error.Validation(
            "CashMovementTypes.RowVersionRequired",
            "يجب إرسال إصدار نوع الحركة النقدية الحالي للتعديل.",
            nameof(CashMovementTypeUpdateRequest.RowVersion));

    public static Error Concurrency() =>
        Error.Conflict(
            "CashMovementTypes.Concurrency",
            "تم تعديل نوع الحركة النقدية بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");

    public static Error HasVouchers() =>
        Error.Conflict(
            "CashMovementTypes.HasVouchers",
            "لا يمكن حذف نوع الحركة النقدية لارتباطه بسندات حالية أو تاريخية. يمكن إلغاء تنشيطه بدلاً من ذلك.");

    public static Error UsedSemanticsChangeNotAllowed() =>
        Error.Conflict(
            "CashMovementTypes.UsedSemanticsChangeNotAllowed",
            "لا يمكن تغيير اتجاه أو تصنيف أو ارتباط نوع الحركة بعميل أو مورد بعد استخدامه في سند نقدية.");
}
