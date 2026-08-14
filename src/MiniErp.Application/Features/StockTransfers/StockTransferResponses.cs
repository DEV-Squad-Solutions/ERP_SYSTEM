namespace MiniErp.Application.Features.StockTransfers;

public sealed record StockTransferLineResponse(
    int Id,
    int ItemId,
    string ItemCode,
    string ItemName,
    int ItemUnitId,
    string ItemUnitName,
    decimal Quantity,
    string? Notes,
    int SourceMovementId,
    decimal SourceUnitCost,
    decimal SourceTotalCost,
    decimal SourceQuantityAfter,
    decimal SourceAverageCostAfter,
    decimal SourceInventoryValueAfter,
    int DestinationMovementId,
    decimal DestinationUnitCost,
    decimal DestinationTotalCost,
    decimal DestinationQuantityAfter,
    decimal DestinationAverageCostAfter,
    decimal DestinationInventoryValueAfter);

public sealed record StockTransferResponse(
    int Id,
    int CompanyId,
    string DocumentNumber,
    DateOnly TransferDate,
    int SourceStoreId,
    string SourceStoreName,
    int DestinationStoreId,
    string DestinationStoreName,
    string? Notes,
    DateTime LastModifiedAt,
    byte[] RowVersion,
    IReadOnlyList<StockTransferLineResponse> Lines);

public sealed record StockTransferListResponse(
    int Id,
    int CompanyId,
    string DocumentNumber,
    DateOnly TransferDate,
    int SourceStoreId,
    string SourceStoreName,
    int DestinationStoreId,
    string DestinationStoreName,
    string? Notes,
    DateTime LastModifiedAt,
    int LineCount,
    decimal TotalQuantity,
    byte[] RowVersion);
