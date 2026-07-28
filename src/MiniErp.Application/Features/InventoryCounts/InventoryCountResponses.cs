namespace MiniErp.Application.Features.InventoryCounts;

public sealed record InventoryCountLineResponse(
    int Id,
    int CompanyId,
    int InventoryCountId,
    int ItemId,
    string ItemCode,
    string ItemName,
    int ItemUnitId,
    string ItemUnitName,
    decimal SystemQuantity,
    decimal? PhysicalQuantity,
    decimal? Difference,
    string? Notes);

public sealed record InventoryCountListResponse(
    int Id,
    int CompanyId,
    int StoreId,
    string StoreName,
    string DocumentNumber,
    DateOnly CountDate,
    DateTime SnapshotTakenAt,
    DateTime? ReconciledAt,
    string? Notes,
    DateTime LastModifiedAt,
    int LineCount,
    int CountedLineCount,
    int DifferenceLineCount,
    int? IncreaseAdjustmentId,
    int? DecreaseAdjustmentId,
    byte[] RowVersion);

public sealed record InventoryCountResponse(
    int Id,
    int CompanyId,
    int StoreId,
    string StoreName,
    string DocumentNumber,
    DateOnly CountDate,
    DateTime SnapshotTakenAt,
    DateTime? ReconciledAt,
    string? Notes,
    DateTime LastModifiedAt,
    int? IncreaseAdjustmentId,
    int? DecreaseAdjustmentId,
    byte[] RowVersion,
    IReadOnlyList<InventoryCountLineResponse> Lines);
