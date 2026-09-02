using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.ExchangeRates;

public interface IExchangeRatePostingSynchronizer
{
    Task<Result> SynchronizeAsync(
        int exchangeRateId,
        CancellationToken cancellationToken = default);
}
