namespace MiniErp.Application.Features.StockTransfers;

public sealed record StockTransferLineRequest(
    int ItemId,
    decimal Quantity,
    string? Notes);

public sealed record StockTransferRequest(
    DateOnly TransferDate,
    int SourceStoreId,
    int DestinationStoreId,
    string? Notes,
    IReadOnlyList<StockTransferLineRequest> Lines)
{
    public const int NotesMaximumLength = 1_000;

    public const int MaximumLineCount = 100;
}

public sealed record StockTransferUpdateRequest(
    DateOnly TransferDate,
    string? Notes,
    IReadOnlyList<StockTransferLineRequest> Lines,
    byte[]? RowVersion);

public sealed record StockTransferFilterRequest(
    string? Search = null,
    int? SourceStoreId = null,
    int? DestinationStoreId = null,
    int? ItemId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null)
{
    public const int SearchMaximumLength = 50;
}
