using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ExchangeRates;

public sealed record ExchangeRateRequest(
    CurrencyCode Currency,
    DateOnly RateDate,
    decimal Rate,
    ExchangeRateSource Source = ExchangeRateSource.Manual,
    string? Notes = null);

public sealed record ExchangeRateUpdateRequest(
    CurrencyCode Currency,
    DateOnly RateDate,
    decimal Rate,
    ExchangeRateSource Source,
    string? Notes,
    byte[]? RowVersion,
    bool UpdateLinkedTransactions = false);
