using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public sealed record PartnerOpeningBalanceRequest(
    int BusinessPartnerId,
    string DocumentNumber,
    DateOnly DocumentDate,
    CurrencyCode Currency,
    PartnerBalanceType BalanceType,
    decimal Amount,
    string? Notes)
{
    public const int DocumentNumberMaximumLength = 50;

    public const int NotesMaximumLength = 1_000;
}

public sealed record PartnerOpeningBalanceUpdateRequest(
    int BusinessPartnerId,
    string DocumentNumber,
    DateOnly DocumentDate,
    CurrencyCode Currency,
    PartnerBalanceType BalanceType,
    decimal Amount,
    string? Notes,
    byte[]? RowVersion);
