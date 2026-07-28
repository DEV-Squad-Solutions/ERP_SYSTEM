using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public sealed record PartnerOpeningBalanceFilterRequest(
    string? DocumentNumber = null,
    int? BusinessPartnerId = null,
    CurrencyCode? Currency = null,
    PartnerBalanceType? BalanceType = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);
