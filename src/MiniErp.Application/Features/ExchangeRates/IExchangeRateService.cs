using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ExchangeRates;

public interface IExchangeRateService
{
    Task<Result<PagedResponse<ExchangeRateResponse>>> GetAllAsync(
        PaginationRequest pagination,
        ExchangeRateFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeRateResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeRateResolutionResponse>> ResolveAsync(
        CurrencyCode currency,
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeRateResponse>> AddAsync(
        ExchangeRateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeRateResponse>> UpdateAsync(
        int id,
        ExchangeRateUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}

public interface IExchangeRateResolver
{
    Task<Result<ResolvedExchangeRate>> ResolveAsync(
        CurrencyCode currency,
        DateOnly date,
        decimal? requestedRate = null,
        CancellationToken cancellationToken = default);
}
