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
    string? Reason);

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
