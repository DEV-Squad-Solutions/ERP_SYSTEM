using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ExchangeRates;

public interface IExchangeRateResolver
{
    Task<Result<ResolvedExchangeRate>> ResolveAsync(
        CurrencyCode currency,
        DateOnly date,
        decimal? requestedRate = null,
        CancellationToken cancellationToken = default);
}
