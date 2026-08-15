using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ExchangeRates;

public sealed record ExchangeRateFilterRequest(
    CurrencyCode? Currency = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    ExchangeRateSource? Source = null,
    string? Search = null);
