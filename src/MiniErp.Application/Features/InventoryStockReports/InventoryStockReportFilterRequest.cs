namespace MiniErp.Application.Features.InventoryStockReports;

public sealed record InventoryStockReportFilterRequest(
    int StoreId,
    DateOnly? AsOfDate = null,
    string? Search = null,
    int? ItemId = null,
    int? ItemUnitId = null,
    bool? HasStock = null);
