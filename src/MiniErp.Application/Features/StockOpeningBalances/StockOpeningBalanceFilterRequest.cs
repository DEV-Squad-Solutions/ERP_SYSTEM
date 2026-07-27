namespace MiniErp.Application.Features.StockOpeningBalances;

public sealed record StockOpeningBalanceFilterRequest(
    string? DocumentNumber = null,
    int? StoreId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);
