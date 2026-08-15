using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.ItemsCategories;

public static class ItemsCategoryErrors
{
    public static Error InvalidId() =>
        Error.Validation(
            "ItemsCategories.InvalidId",
            "يجب أن يكون رقم تصنيف الأصناف أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "ItemsCategories.NotFound",
            $"لم يتم العثور على تصنيف الأصناف رقم {id}.");

    public static Error NameExists(string name) =>
        Error.Conflict(
            "ItemsCategories.NameExists",
            $"تصنيف الأصناف النشط '{name}' موجود بالفعل.",
            nameof(ItemsCategoryRequest.Name));

    public static Error RowVersionRequired() =>
        Error.Validation(
            "ItemsCategories.RowVersionRequired",
            "يجب إرسال إصدار تصنيف الأصناف الحالي للتعديل.",
            nameof(ItemsCategoryUpdateRequest.RowVersion));

    public static Error Concurrency() =>
        Error.Conflict(
            "ItemsCategories.Concurrency",
            "تم تعديل تصنيف الأصناف بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");

    public static Error HasInvoices() =>
        Error.Conflict(
            "ItemsCategories.HasInvoices",
            "لا يمكن حذف تصنيف الأصناف لارتباطه بفواتير حالية أو تاريخية. يمكن إلغاء تنشيطه بدلاً من ذلك.");
}
