using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests;

internal sealed class TestExchangeRateResolver(
    CurrencyCode baseCurrency = CurrencyCode.EGP)
    : IExchangeRateResolver
{
    public Task<Result<ResolvedExchangeRate>> ResolveAsync(
        CurrencyCode currency,
        DateOnly date,
        decimal? requestedRate = null,
        CancellationToken cancellationToken = default)
    {
        var isBaseCurrency = currency == baseCurrency;
        var rate = isBaseCurrency ? 1m : requestedRate ?? 1m;

        return Task.FromResult(
            Result<ResolvedExchangeRate>.Success(
                new ResolvedExchangeRate(
                    null,
                    baseCurrency,
                    currency,
                    date,
                    isBaseCurrency ? null : date,
                    rate,
                    isBaseCurrency
                        ? null
                        : ExchangeRateSource.Manual,
                    isBaseCurrency)));
    }
}
