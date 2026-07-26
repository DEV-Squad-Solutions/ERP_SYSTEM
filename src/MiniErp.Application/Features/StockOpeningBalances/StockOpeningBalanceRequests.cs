namespace MiniErp.Application.Features.StockOpeningBalances;

public sealed record StockOpeningBalanceLineRequest(
    int ItemId,
    int Count,
    decimal Weight,
    decimal Price,
    string? Notes);

public sealed record StockOpeningBalanceRequest(
    int StoreId,
    string DocumentNumber,
    DateOnly DocumentDate,
    IReadOnlyList<StockOpeningBalanceLineRequest> Lines,
    string? Notes)
{
    public const int DocumentNumberMaximumLength = 50;

    public const int MaximumLineCount = 100;

    public const int NotesMaximumLength = 1_000;

}

public sealed record StockOpeningBalanceUpdateRequest(
    int StoreId,
    string DocumentNumber,
    DateOnly DocumentDate,
    IReadOnlyList<StockOpeningBalanceLineRequest> Lines,
    string? Notes,
    byte[]? RowVersion);
