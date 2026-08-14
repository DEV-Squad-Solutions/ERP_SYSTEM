using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.InventoryStockReports;

public sealed record InventoryStockReportItemResponse(
    int ItemId,
    string ItemCode,
    string ItemName,
    int ItemUnitId,
    string ItemUnitName,
    decimal Balance,
    decimal AverageCost,
    decimal InventoryValue);

public sealed record InventoryStockReportSummaryResponse(
    int TotalItemCount,
    int ItemsWithStockCount,
    decimal TotalInventoryValue);

public sealed record InventoryStockReportResponse(
    int StoreId,
    string StoreCode,
    string StoreName,
    DateOnly AsOfDate,
    CurrencyCode BaseCurrency,
    IReadOnlyList<InventoryStockReportItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    InventoryStockReportSummaryResponse Summary);
