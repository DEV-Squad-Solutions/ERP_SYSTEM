using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ExchangeRates;

public interface IExchangeRateProvider
{
    string Name { get; }

    Task<Result<ExternalExchangeRate>> GetRateAsync(
        CurrencyCode currency,
        CurrencyCode baseCurrency,
        DateOnly requestedDate,
        CancellationToken cancellationToken = default);
}

public sealed record ExternalExchangeRate(
    CurrencyCode Currency,
    CurrencyCode BaseCurrency,
    DateOnly RequestedDate,
    DateOnly RateDate,
    decimal Rate,
    string Provider);
