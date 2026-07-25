using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public interface IPartnerOpeningBalanceRequest
{
    int BusinessPartnerId { get; }

    string DocumentNumber { get; }

    DateOnly DocumentDate { get; }

    CurrencyCode Currency { get; }

    PartnerBalanceType BalanceType { get; }

    decimal Amount { get; }

    string? Notes { get; }
}

public sealed record PartnerOpeningBalanceRequest(
    int BusinessPartnerId,
    string DocumentNumber,
    DateOnly DocumentDate,
    CurrencyCode Currency,
    PartnerBalanceType BalanceType,
    decimal Amount,
    string? Notes) : IPartnerOpeningBalanceRequest
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
    byte[]? RowVersion) : IPartnerOpeningBalanceRequest;
