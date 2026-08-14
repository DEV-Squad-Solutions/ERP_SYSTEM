using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.StockTransfers;

public static class StockTransferErrors
{
    public static Error InvalidId() =>
        Error.Validation(
            "StockTransfers.InvalidId",
            "رقم تحويل المخزون غير صحيح.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "StockTransfers.NotFound",
            $"لم يتم العثور على تحويل المخزون رقم {id}.");

    public static Error DocumentNumberExists(string number) =>
        Error.Conflict(
            "StockTransfers.DocumentNumberExists",
            $"رقم تحويل المخزون '{number}' مستخدم بالفعل.",
            "DocumentNumber");

    public static Error StoresMustDiffer() =>
        Error.Validation(
            "StockTransfers.StoresMustDiffer",
            "يجب اختيار مخزن وجهة مختلف عن مخزن المصدر.",
            nameof(StockTransferRequest.DestinationStoreId));

    public static Error StoreNotFound(int id, string fieldName) =>
        Error.NotFound(
            "StockTransfers.StoreNotFound",
            $"لم يتم العثور على المخزن رقم {id}.",
            fieldName);

    public static Error StoreInactive(int id, string fieldName) =>
        Error.Conflict(
            "StockTransfers.StoreInactive",
            $"المخزن رقم {id} غير نشط.",
            fieldName);

    public static Error ContainerStoreNotAllowed(int id, string fieldName) =>
        Error.Conflict(
            "StockTransfers.ContainerStoreNotAllowed",
            $"المخزن رقم {id} مخصص للعبوات ولا يمكن استخدامه لتحويل الأصناف.",
            fieldName);

    public static Error ItemNotFound(IEnumerable<int> ids) =>
        Error.NotFound(
            "StockTransfers.ItemNotFound",
            $"لم يتم العثور على الأصناف: {string.Join(", ", ids)}.",
            nameof(StockTransferLineRequest.ItemId));

    public static Error ItemInactive(IEnumerable<int> ids) =>
        Error.Conflict(
            "StockTransfers.ItemInactive",
            $"لا يمكن تحويل الأصناف غير النشطة: {string.Join(", ", ids)}.",
            nameof(StockTransferLineRequest.ItemId));

    public static Error ItemUnitInactive(IEnumerable<int> ids) =>
        Error.Conflict(
            "StockTransfers.ItemUnitInactive",
            $"وحدات قياس الأصناف التالية غير نشطة: {string.Join(", ", ids)}.",
            nameof(StockTransferLineRequest.ItemId));

    public static Error RowVersionRequired() =>
        Error.Validation(
            "StockTransfers.RowVersionRequired",
            "أعد تحميل تحويل المخزون ثم حاول التعديل مرة أخرى.",
            nameof(StockTransferUpdateRequest.RowVersion));

    public static Error Concurrency() =>
        Error.Conflict(
            "StockTransfers.Concurrency",
            "تم تعديل تحويل المخزون بواسطة مستخدم آخر. أعد تحميله ثم حاول مرة أخرى.");

    public static Error FiltersInvalid() =>
        Error.Validation(
            "StockTransfers.FiltersInvalid",
            "مرشحات تحويلات المخزون غير صحيحة.");
}
