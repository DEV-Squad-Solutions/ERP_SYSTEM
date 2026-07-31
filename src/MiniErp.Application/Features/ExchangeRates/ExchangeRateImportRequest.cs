using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ExchangeRates;

public sealed record ExchangeRateImportRequest(
    DateOnly RateDate,
    IReadOnlyCollection<CurrencyCode>? Currencies = null,
    bool ReplaceUnreferencedImportedRates = false);
