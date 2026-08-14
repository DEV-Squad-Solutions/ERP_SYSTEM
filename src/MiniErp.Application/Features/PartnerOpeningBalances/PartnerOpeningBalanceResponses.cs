using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public sealed record PartnerOpeningBalanceResponse(
    int Id,
    int CompanyId,
    int BusinessPartnerId,
    string BusinessPartnerName,
    string DocumentNumber,
    DateOnly DocumentDate,
    CurrencyCode Currency,
    CurrencyCode BaseCurrency,
    decimal ExchangeRate,
    PartnerBalanceType BalanceType,
    decimal Amount,
    decimal BaseAmount,
    string? Notes,
    byte[] RowVersion);
