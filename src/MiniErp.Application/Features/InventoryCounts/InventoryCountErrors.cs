using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.InventoryCounts;

public static class InventoryCountErrors
{
    public static Error StoreNotFound(int id) =>
        Error.NotFound(
            "InventoryCounts.StoreNotFound",
            $"لم يتم العثور على المخزن رقم {id}.",
            nameof(InventoryCountRequest.StoreId));

    public static Error StoreInactive() =>
        Error.Conflict(
            "InventoryCounts.StoreInactive",
            "لا يمكن جرد مخزن غير نشط.",
            nameof(InventoryCountRequest.StoreId));

    public static Error ContainerStoreNotAllowed() =>
        Error.Conflict(
            "InventoryCounts.ContainerStoreNotAllowed",
            "يجب اختيار مخزن منتجات وليس مخزن عبوات.",
            nameof(InventoryCountRequest.StoreId));

    public static Error FiltersInvalid() =>
        Error.Validation(
            "InventoryCounts.FiltersInvalid",
            "مرشحات مستندات الجرد غير صحيحة.");

    public static Error InvalidId() =>
        Error.Validation(
            "InventoryCounts.InvalidId",
            "يجب أن يكون رقم مستند الجرد أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "InventoryCounts.NotFound",
            $"لم يتم العثور على مستند الجرد رقم {id}.");

    public static Error RowVersionRequired() =>
        Error.Validation(
            "InventoryCounts.RowVersionRequired",
            "يجب إرسال إصدار السجل الحالي المكون من 8 بايت للتعديل.",
            nameof(InventoryCountUpdateRequest.RowVersion));

    public static Error ReconcileRowVersionRequired() =>
        Error.Validation(
            "InventoryCounts.RowVersionRequired",
            "يجب إرسال إصدار السجل الحالي المكون من 8 بايت للتسوية.",
            nameof(InventoryCountReconcileRequest.RowVersion));

    public static Error Concurrency() =>
        Error.Conflict(
            "InventoryCounts.Concurrency",
            "تم تعديل مستند الجرد بواسطة مستخدم آخر. أعد تحميله ثم حاول مرة أخرى.");

    public static Error DocumentNumberExists(string number) =>
        Error.Conflict(
            "InventoryCounts.DocumentNumberExists",
            $"رقم مستند الجرد '{number}' مستخدم بالفعل.",
            nameof(InventoryCountRequest.DocumentNumber));

    public static Error NoEligibleItems() =>
        Error.Conflict(
            "InventoryCounts.NoEligibleItems",
            "لا توجد أصناف نشطة بوحدات قياس نشطة لإنشاء مستند الجرد.");

    public static Error LinesDoNotMatchSnapshot() =>
        Error.Validation(
            "InventoryCounts.LinesDoNotMatchSnapshot",
            "يجب إرسال مجموعة أصناف لقطة الجرد كاملة دون إضافة أو حذف أو تكرار.",
            nameof(InventoryCountUpdateRequest.Lines));

    public static Error ReconciledImmutable() =>
        Error.Conflict(
            "InventoryCounts.ReconciledImmutable",
            "مستند الجرد الذي تمت تسويته غير قابل للتعديل أو الحذف.");

    public static Error AlreadyReconciled() =>
        Error.Conflict(
            "InventoryCounts.AlreadyReconciled",
            "تمت تسوية مستند الجرد بالفعل.");

    public static Error PhysicalQuantitiesRequired(IEnumerable<int> itemIds) =>
        Error.Validation(
            "InventoryCounts.PhysicalQuantitiesRequired",
            $"يجب إدخال الكمية الفعلية لكل الأصناف. الأصناف الناقصة: {string.Join(", ", itemIds)}.",
            nameof(InventoryCountUpdateRequest.Lines));

    public static Error SnapshotStale() =>
        Error.Conflict(
            "InventoryCounts.SnapshotStale",
            "تغير رصيد المخزون بعد أخذ لقطة الجرد. أنشئ مستند جرد جديدًا ثم أعد العد.");

    public static Error IncreaseCostsInvalid() =>
        Error.Validation(
            "InventoryCounts.IncreaseCostsInvalid",
            "تكاليف زيادات تسوية الجرد غير صالحة أو تحتوي على أصناف مكررة.",
            nameof(InventoryCountReconcileRequest.IncreaseCosts));

    public static Error IncreaseCostsRequired(IEnumerable<int> itemIds) =>
        Error.Validation(
            "InventoryCounts.IncreaseCostsRequired",
            $"يجب إدخال تكلفة الوحدة لكل صنف له زيادة في تسوية الجرد: {string.Join(", ", itemIds)}.",
            nameof(InventoryCountReconcileRequest.IncreaseCosts));

    public static Error GeneratedDocumentNumberConflict() =>
        Error.Conflict(
            "InventoryCounts.GeneratedDocumentNumberConflict",
            "تعذر إنشاء أرقام مستندات التسوية الخاصة بالجرد لأنها مستخدمة بالفعل.");
}
