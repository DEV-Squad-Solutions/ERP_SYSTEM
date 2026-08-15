using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.StockAdjustments;

public static class StockAdjustmentErrors
{
    public static Error DirectionInvalid() =>
        Error.Validation(
            "StockAdjustments.DirectionInvalid",
            "اتجاه تسوية المخزون غير مدعوم.",
            nameof(StockAdjustmentRequest.Direction));

    public static Error LinesInvalid() =>
        Error.Validation(
            "StockAdjustments.LinesInvalid",
            "يجب إرسال سطور تسوية صحيحة بأصناف غير مكررة وكميات موجبة.",
            nameof(StockAdjustmentRequest.Lines));

    public static Error UnitCostRequired() =>
        Error.Validation(
            "StockAdjustments.UnitCostRequired",
            "يجب إدخال تكلفة الوحدة لكل سطر عند زيادة المخزون.",
            nameof(StockAdjustmentLineRequest.UnitCost));

    public static Error UnitCostNotAllowed() =>
        Error.Validation(
            "StockAdjustments.UnitCostNotAllowed",
            "لا يجوز إدخال تكلفة الوحدة في تسوية الخصم؛ يستخدم الخادم متوسط التكلفة الحالي.",
            nameof(StockAdjustmentLineRequest.UnitCost));

    public static Error UnitCostInvalid() =>
        Error.Validation(
            "StockAdjustments.UnitCostInvalid",
            "يجب ألا تقل تكلفة الوحدة عن صفر.",
            nameof(StockAdjustmentLineRequest.UnitCost));

    public static Error FiltersInvalid() =>
        Error.Validation(
            "StockAdjustments.FiltersInvalid",
            "مرشحات تسويات المخزون غير صحيحة.");

    public static Error InvalidId() =>
        Error.Validation(
            "StockAdjustments.InvalidId",
            "يجب أن يكون رقم تسوية المخزون أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "StockAdjustments.NotFound",
            $"لم يتم العثور على تسوية المخزون رقم {id}.");

    public static Error RowVersionRequired() =>
        Error.Validation(
            "StockAdjustments.RowVersionRequired",
            "يجب إرسال إصدار السجل الحالي المكون من 8 بايت للتعديل.",
            nameof(StockAdjustmentUpdateRequest.RowVersion));

    public static Error Concurrency() =>
        Error.Conflict(
            "StockAdjustments.Concurrency",
            "تم تعديل تسوية المخزون بواسطة مستخدم آخر. أعد تحميلها ثم حاول مرة أخرى.");

    public static Error DocumentNumberExists(string number) =>
        Error.Conflict(
            "StockAdjustments.DocumentNumberExists",
            $"رقم مستند التسوية '{number}' مستخدم بالفعل.",
            "DocumentNumber");

    public static Error StoreNotFound(int id) =>
        Error.NotFound(
            "StockAdjustments.StoreNotFound",
            $"لم يتم العثور على المخزن رقم {id}.",
            nameof(StockAdjustmentRequest.StoreId));

    public static Error StoreInactive() =>
        Error.Conflict(
            "StockAdjustments.StoreInactive",
            "لا يمكن استخدام مخزن غير نشط.",
            nameof(StockAdjustmentRequest.StoreId));

    public static Error ContainerStoreNotAllowed() =>
        Error.Conflict(
            "StockAdjustments.ContainerStoreNotAllowed",
            "يجب اختيار مخزن منتجات وليس مخزن عبوات.",
            nameof(StockAdjustmentRequest.StoreId));

    public static Error ItemNotFound(IEnumerable<int> ids) =>
        Error.NotFound(
            "StockAdjustments.ItemNotFound",
            $"لم يتم العثور على الأصناف: {string.Join(", ", ids)}.",
            nameof(StockAdjustmentLineRequest.ItemId));

    public static Error ItemInactive(IEnumerable<int> ids) =>
        Error.Conflict(
            "StockAdjustments.ItemInactive",
            $"لا يمكن استخدام الأصناف غير النشطة: {string.Join(", ", ids)}.",
            nameof(StockAdjustmentLineRequest.ItemId));

    public static Error ItemUnitInactive(IEnumerable<int> ids) =>
        Error.Conflict(
            "StockAdjustments.ItemUnitInactive",
            $"وحدات قياس الأصناف التالية غير نشطة: {string.Join(", ", ids)}.",
            nameof(StockAdjustmentLineRequest.ItemId));

    public static Error GeneratedAdjustmentImmutable() =>
        Error.Conflict(
            "StockAdjustments.GeneratedAdjustmentImmutable",
            "تسوية المخزون المنشأة من مستند جرد غير قابلة للتعديل أو الحذف.");
}
