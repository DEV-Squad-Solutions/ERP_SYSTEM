using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ExchangeRates;

public sealed record ExchangeRateResponse(
    int Id,
    int CompanyId,
    CurrencyCode BaseCurrency,
    CurrencyCode Currency,
    DateOnly RateDate,
    decimal Rate,
    ExchangeRateSource Source,
    string? Notes,
    byte[] RowVersion);

public sealed record ExchangeRateResolutionResponse(
    int? ExchangeRateId,
    CurrencyCode BaseCurrency,
    CurrencyCode Currency,
    DateOnly RequestedDate,
    DateOnly? RateDate,
    decimal Rate,
    ExchangeRateSource? Source,
    bool IsBaseCurrency);

public sealed record ResolvedExchangeRate(
    int? ExchangeRateId,
    CurrencyCode BaseCurrency,
    CurrencyCode Currency,
    DateOnly RequestedDate,
    DateOnly? RateDate,
    decimal Rate,
    ExchangeRateSource? Source,
    bool IsBaseCurrency);
