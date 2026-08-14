using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Cashboxes;

public sealed record CashboxResponse(
    int Id,
    int CompanyId,
    string Code,
    string Name,
    CurrencyCode Currency,
    decimal OpeningBalance,
    DateOnly OpeningBalanceDate,
    CurrencyCode BaseCurrency,
    decimal OpeningExchangeRate,
    decimal BaseOpeningBalance,
    decimal CurrentBalance,
    bool IsActive,
    string? Notes,
    byte[] RowVersion);

public sealed record CashboxSelectResponse(
    int Id,
    string Name,
    CurrencyCode Currency,
    decimal CurrentBalance);
