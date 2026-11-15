using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Cashboxes;

public static class CashboxErrors
{
    public static Error CodeExists(string code) =>
        Error.Conflict(
            "Cashboxes.CodeExists",
            $"كود صندوق النقدية '{code}' مستخدم بالفعل.",
            "Code");

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
            "لا يمكن حذف صندوق النقدية لوجود سندات نقدية غير محذوفة عليه. يمكن إلغاء تنشيطه بعد تصفير رصيده.");

    public static Error NonZeroBalance() =>
        Error.Conflict(
            "Cashboxes.NonZeroBalance",
            "لا يمكن إلغاء تنشيط الخزينة إلا بعد أن يصبح رصيدها الحالي في السنة المالية الحالية صفراً بعملة الخزينة.",
            nameof(CashboxUpdateRequest.IsActive));

    public static Error HasMovements() =>
        Error.Conflict(
            "Cashboxes.HasMovements",
            "لا يمكن حذف الخزينة لوجود تحويلات أو قيود مالية قائمة مرتبطة بها.");

    public static Error OpeningOrCurrencyChangeNotAllowed() =>
        Error.Conflict(
            "Cashboxes.OpeningOrCurrencyChangeNotAllowed",
            "لا يمكن تغيير الرصيد الافتتاحي أو العملة بعد إنشاء سندات على صندوق النقدية.");

    public static Error InvalidLinkedTransfer() =>
        Error.Conflict(
            "Cashboxes.InvalidLinkedTransfer",
            "تعذر تحديث سعر الصرف لأن أحد تحويلات الخزائن المرتبطة لا يحتوي على سند صرف وقبض مكتملين.");
}
