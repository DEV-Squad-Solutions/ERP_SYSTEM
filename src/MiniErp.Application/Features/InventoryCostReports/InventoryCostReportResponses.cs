using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.InventoryCostReports;

public sealed record InventoryCostAllocationReportResponse(
    long Id,
    bool IsInboundAllocation,
    int RelatedMovementId,
    DateOnly RelatedMovementDate,
    ItemMovementType RelatedMovementType,
    string RelatedReferenceNumber,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalCost);

public sealed record InventoryCostReportItemResponse(
    int MovementId,
    DateOnly MovementDate,
    DateTime CreatedOn,
    ItemMovementType MovementType,
    int ReferenceId,
    string ReferenceNumber,
    string? Description,
    decimal QuantityIn,
    decimal QuantityOut,
    InventoryCostStatus CostStatus,
    decimal PendingCostQuantity,
    decimal? UnitCost,
    decimal TotalCost,
    decimal QuantityAfter,
    decimal AverageCostAfter,
    decimal InventoryValueAfter,
    IReadOnlyList<InventoryCostAllocationReportResponse> Allocations);

public sealed record InventoryCostReportSummaryResponse(
    decimal OpeningQuantity,
    decimal OpeningAverageCost,
    decimal OpeningInventoryValue,
    decimal TotalQuantityIn,
    decimal TotalQuantityOut,
    decimal TotalInboundCost,
    decimal TotalOutboundCost,
    decimal ClosingQuantity,
    decimal ClosingAverageCost,
    decimal ClosingInventoryValue,
    decimal CurrentQuantity,
    decimal CurrentAverageCost,
    decimal CurrentInventoryValue,
    decimal PendingCostQuantity,
    int PendingMovementCount,
    int RevaluedMovementCount);

public sealed record InventoryCostReportResponse(
    int StoreId,
    string StoreCode,
    string StoreName,
    int ItemId,
    string ItemCode,
    string ItemName,
    string ItemUnitName,
    CurrencyCode BaseCurrency,
    DateOnly? FromDate,
    DateOnly? ToDate,
    IReadOnlyList<InventoryCostReportItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    InventoryCostReportSummaryResponse Summary);
