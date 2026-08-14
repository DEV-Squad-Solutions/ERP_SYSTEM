using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.StockOpeningBalances;

public static class StockOpeningBalanceErrors
{
    public static Error RowVersionRequired() =>
        Error.Validation(
            "StockOpeningBalances.RowVersionRequired",
            "يجب إرسال إصدار السجل الحالي للتعديل.",
            nameof(StockOpeningBalanceUpdateRequest.RowVersion));

    public static Error StoreNotFound(int id) =>
        Error.NotFound(
            "StockOpeningBalances.StoreNotFound",
            $"لم يتم العثور على المخزن رقم {id}.",
            nameof(StockOpeningBalanceRequest.StoreId));

    public static Error ContainerStoreNotAllowed() =>
        Error.Conflict(
            "StockOpeningBalances.ContainerStoreNotAllowed",
            "يجب اختيار مخزن منتجات وليس مخزن عبوات.",
            nameof(StockOpeningBalanceRequest.StoreId));

    public static Error StoreInactive() =>
        Error.Conflict(
            "StockOpeningBalances.StoreInactive",
            "لا يمكن استخدام مخزن غير نشط.",
            nameof(StockOpeningBalanceRequest.StoreId));

    public static Error ItemNotFound(IEnumerable<int> ids) =>
        Error.NotFound(
            "StockOpeningBalances.ItemNotFound",
            $"لم يتم العثور على الأصناف ذات الأرقام: {string.Join(", ", ids)}.",
            nameof(StockOpeningBalanceLineRequest.ItemId));

    public static Error ItemInactive(IEnumerable<int> ids) =>
        Error.Conflict(
            "StockOpeningBalances.ItemInactive",
            $"لا يمكن استخدام الأصناف غير النشطة: {string.Join(", ", ids)}.",
            nameof(StockOpeningBalanceLineRequest.ItemId));

    public static Error ItemUnitInactive(IEnumerable<int> itemIds) =>
        Error.Conflict(
            "StockOpeningBalances.ItemUnitInactive",
            $"وحدات قياس الأصناف التالية غير نشطة: {string.Join(", ", itemIds)}.",
            nameof(StockOpeningBalanceLineRequest.ItemId));

    public static Error InvalidId() =>
        Error.Validation(
            "StockOpeningBalances.InvalidId",
            "يجب أن يكون رقم الرصيد الافتتاحي أكبر من صفر.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "StockOpeningBalances.NotFound",
            $"لم يتم العثور على الرصيد الافتتاحي رقم {id}.");

    public static Error DocumentNumberExists(string number) =>
        Error.Conflict(
            "StockOpeningBalances.DocumentNumberExists",
            $"رقم المستند '{number}' مستخدم بالفعل.",
            "DocumentNumber");

    public static Error Concurrency() =>
        Error.Conflict(
            "StockOpeningBalances.Concurrency",
            "تم تعديل المستند بواسطة عملية أخرى؛ أعد تحميله ثم حاول مرة أخرى.");
}
