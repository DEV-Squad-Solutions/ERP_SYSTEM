using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.InventoryCostReports;

public static class InventoryCostReportErrors
{
    public static Error StoreRequired() =>
        Error.Validation(
            "InventoryCostReports.StoreRequired",
            "يجب اختيار مخزن صالح لتقرير متوسط التكلفة.",
            nameof(InventoryCostReportFilterRequest.StoreId));

    public static Error ItemRequired() =>
        Error.Validation(
            "InventoryCostReports.ItemRequired",
            "يجب اختيار صنف صالح لتقرير متوسط التكلفة.",
            nameof(InventoryCostReportFilterRequest.ItemId));

    public static Error StoreNotFound() =>
        Error.NotFound(
            "InventoryCostReports.StoreNotFound",
            "مخزن تقرير متوسط التكلفة غير موجود أو لا ينتمي إلى الشركة الحالية.",
            nameof(InventoryCostReportFilterRequest.StoreId));

    public static Error ProductStoreRequired() =>
        Error.Conflict(
            "InventoryCostReports.ProductStoreRequired",
            "تقرير متوسط تكلفة الأصناف متاح لمخازن المنتجات فقط.",
            nameof(InventoryCostReportFilterRequest.StoreId));

    public static Error ItemNotFound() =>
        Error.NotFound(
            "InventoryCostReports.ItemNotFound",
            "الصنف غير موجود أو لا ينتمي إلى الشركة الحالية.",
            nameof(InventoryCostReportFilterRequest.ItemId));
}
