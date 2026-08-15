using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public sealed record PartnerOpeningBalanceRequest(
    int BusinessPartnerId,
    DateOnly DocumentDate,
    CurrencyCode Currency,
    PartnerBalanceType BalanceType,
    decimal Amount,
    string? Notes,
    decimal? ExchangeRate = null)
{
    public const int NotesMaximumLength = 1_000;
}

public sealed record PartnerOpeningBalanceUpdateRequest(
    int BusinessPartnerId,
    DateOnly DocumentDate,
    CurrencyCode Currency,
    PartnerBalanceType BalanceType,
    decimal Amount,
    string? Notes,
    byte[]? RowVersion,
    decimal? ExchangeRate = null);
