namespace MiniErp.Application.Features.StockOpeningBalances;

public sealed record StockOpeningBalanceFilterRequest(
    int? FiscalYearId = null,
    string? DocumentNumber = null,
    int? StoreId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);
