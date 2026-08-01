using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Cashboxes;

public static class CashboxErrors
{
    public static Error CodeExists(string code) =>
        Error.Conflict(
            "Cashboxes.CodeExists",
            $"كود صندوق النقدية '{code}' مستخدم بالفعل.",
            nameof(CashboxRequest.Code));

    public static Error NameExists(string name) =>
        Error.Conflict(
            "Cashboxes.NameExists",
            $"اسم صندوق النقدية '{name}' مستخدم بالفعل.",
            nameof(CashboxRequest.Name));

    public static Error InvalidId() =>
        Error.Validation(
            "Cashboxes.InvalidId",
            "يجب أن يكون رقم صندوق النقدية أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "Cashboxes.NotFound",
            $"لم يتم العثور على صندوق النقدية رقم {id}.");

    public static Error RowVersionRequired() =>
        Error.Validation(
            "Cashboxes.RowVersionRequired",
            "يجب إرسال إصدار صندوق النقدية الحالي للتعديل.",
            nameof(CashboxUpdateRequest.RowVersion));

    public static Error Concurrency() =>
        Error.Conflict(
            "Cashboxes.Concurrency",
            "تم تعديل صندوق النقدية بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");

    public static Error HasVouchers() =>
        Error.Conflict(
            "Cashboxes.HasVouchers",
            "لا يمكن حذف صندوق النقدية لارتباطه بسندات نقدية حالية أو تاريخية. يمكن إلغاء تنشيطه بدلاً من ذلك.");

    public static Error OpeningOrCurrencyChangeNotAllowed() =>
        Error.Conflict(
            "Cashboxes.OpeningOrCurrencyChangeNotAllowed",
            "لا يمكن تغيير الرصيد الافتتاحي أو العملة بعد إنشاء سندات على صندوق النقدية.");
}
