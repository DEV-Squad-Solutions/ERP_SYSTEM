using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.StockAdjustments;

public sealed record StockAdjustmentLineResponse(
    int Id,
    int CompanyId,
    int StockAdjustmentId,
    int ItemId,
    string ItemCode,
    string ItemName,
    int ItemUnitId,
    string ItemUnitName,
    decimal Quantity,
    decimal? UnitCost,
    string? Reason)
{
    public InventoryCostStatus? CostStatus { get; init; }

    public decimal PendingCostQuantity { get; init; }

    public decimal InventoryTotalCost { get; init; }

    public decimal QuantityAfter { get; init; }

    public decimal AverageCostAfter { get; init; }

    public decimal InventoryValueAfter { get; init; }
}

public sealed record StockAdjustmentResponse(
    int Id,
    int CompanyId,
    int StoreId,
    string StoreName,
    string DocumentNumber,
    DateOnly DocumentDate,
    StockAdjustmentDirection Direction,
    string? Reason,
    int? SourceInventoryCountId,
    DateTime LastModifiedAt,
    byte[] RowVersion,
    IReadOnlyList<StockAdjustmentLineResponse> Lines);

public sealed record StockAdjustmentListResponse(
    int Id,
    int CompanyId,
    int StoreId,
    string StoreName,
    string DocumentNumber,
    DateOnly DocumentDate,
    StockAdjustmentDirection Direction,
    string? Reason,
    int? SourceInventoryCountId,
    DateTime LastModifiedAt,
    int LineCount,
    byte[] RowVersion,
    IReadOnlyList<StockAdjustmentLineResponse> Lines);
