using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.InventoryStockReports;

public static class InventoryStockReportErrors
{
    public static Error StoreRequired() =>
        Error.Validation(
            "InventoryStockReport.StoreRequired",
            "اختر المخزن لعرض تقرير الأرصدة.",
            nameof(InventoryStockReportFilterRequest.StoreId));

    public static Error StoreNotFound() =>
        Error.NotFound(
            "InventoryStockReport.StoreNotFound",
            "المخزن المحدد غير موجود.",
            nameof(InventoryStockReportFilterRequest.StoreId));

    public static Error ProductStoreRequired() =>
        Error.Validation(
            "InventoryStockReport.ProductStoreRequired",
            "اختر مخزن منتجات وليس مخزن عبوات.",
            nameof(InventoryStockReportFilterRequest.StoreId));

    public static Error SearchTooLong() =>
        Error.Validation(
            "InventoryStockReport.SearchTooLong",
            "نص البحث طويل. الحد الأقصى 200 حرف.",
            nameof(InventoryStockReportFilterRequest.Search));

    public static Error ItemInvalid() =>
        Error.Validation(
            "InventoryStockReport.ItemInvalid",
            "رقم الصنف غير صحيح.",
            nameof(InventoryStockReportFilterRequest.ItemId));

    public static Error ItemUnitInvalid() =>
        Error.Validation(
            "InventoryStockReport.ItemUnitInvalid",
            "رقم وحدة القياس غير صحيح.",
            nameof(InventoryStockReportFilterRequest.ItemUnitId));
}
