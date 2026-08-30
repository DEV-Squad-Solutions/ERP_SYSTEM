using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Cashboxes;

public sealed record CashboxRequest(
    string Name,
    CurrencyCode Currency,
    decimal OpeningBalance,
    bool IsActive,
    string? Notes,
    DateOnly? OpeningBalanceDate = null,
    decimal? OpeningExchangeRate = null)
{
    public const int NameMaximumLength = 200;

    public const int NotesMaximumLength = 1_000;
}

public sealed record CashboxUpdateRequest(
    string Name,
    CurrencyCode Currency,
    decimal OpeningBalance,
    bool IsActive,
    string? Notes,
    byte[]? RowVersion,
    DateOnly? OpeningBalanceDate = null,
    decimal? OpeningExchangeRate = null,
    bool UpdateLinkedTransactions = false);
