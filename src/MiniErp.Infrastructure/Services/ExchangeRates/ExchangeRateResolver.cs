using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.ExchangeRates.ExchangeRateErrors;

namespace MiniErp.Infrastructure.Services.ExchangeRates;

public sealed class ExchangeRateResolver(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider)
    : IExchangeRateResolver, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<ResolvedExchangeRate>> ResolveAsync(
        CurrencyCode currency,
        DateOnly date,
        decimal? requestedRate = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(currency))
        {
            return Result<ResolvedExchangeRate>.Failure(InvalidCurrency());
        }

        var baseCurrency = await GetBaseCurrencyAsync(cancellationToken);
        if (currency == baseCurrency)
        {
            if (requestedRate.HasValue && requestedRate.Value != 1m)
            {
                return Result<ResolvedExchangeRate>.Failure(
                    BaseCurrencyRateMustBeOne());
            }

            return Result<ResolvedExchangeRate>.Success(
                new ResolvedExchangeRate(
                    ExchangeRateId: null,
                    BaseCurrency: baseCurrency,
                    Currency: currency,
                    RequestedDate: date,
                    RateDate: null,
                    Rate: 1m,
                    Source: null,
                    IsBaseCurrency: true));
        }

        if (requestedRate.HasValue)
        {
            if (!ExchangeRateRules.IsValidRate(requestedRate.Value))
            {
                return Result<ResolvedExchangeRate>.Failure(InvalidRate());
            }

            var roundedRate = ExchangeRateRules.RoundRate(requestedRate.Value);

            var existingRate = await dbContext.ExchangeRates
                .FirstOrDefaultAsync(entity =>
                    entity.CompanyId == companyId &&
                    entity.Currency == currency &&
                    entity.RateDate == date,
                    cancellationToken);

            int? exchangeRateId = existingRate?.Id;
            if (existingRate is null)
            {
                var persisted = new ExchangeRate
                {
                    CompanyId = companyId,
                    Currency = currency,
                    RateDate = date,
                    Rate = roundedRate,
                    Source = ExchangeRateSource.Manual,
                    Provider = null,
                    Notes = null
                };
                persisted.Touch(timeProvider.GetUtcNow().UtcDateTime);
                dbContext.ExchangeRates.Add(persisted);
                await dbContext.SaveChangesAsync(cancellationToken);
                exchangeRateId = persisted.Id;
            }

            return Result<ResolvedExchangeRate>.Success(
                new ResolvedExchangeRate(
                    ExchangeRateId: exchangeRateId,
                    BaseCurrency: baseCurrency,
                    Currency: currency,
                    RequestedDate: date,
                    RateDate: date,
                    Rate: roundedRate,
                    Source: ExchangeRateSource.Manual,
                    IsBaseCurrency: false));
        }

        var rate = await dbContext.ExchangeRates
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Currency == currency &&
                entity.RateDate <= date)
            .OrderByDescending(entity => entity.RateDate)
            .ThenByDescending(entity => entity.Id)
            .Select(entity => new
            {
                entity.Id,
                entity.RateDate,
                entity.Rate,
                entity.Source
            })
            .FirstOrDefaultAsync(cancellationToken);

        return rate is null
            ? Result<ResolvedExchangeRate>.Failure(Missing(currency, date))
            : Result<ResolvedExchangeRate>.Success(
                new ResolvedExchangeRate(
                    ExchangeRateId: rate.Id,
                    BaseCurrency: baseCurrency,
                    Currency: currency,
                    RequestedDate: date,
                    RateDate: rate.RateDate,
                    Rate: rate.Rate,
                    Source: rate.Source,
                    IsBaseCurrency: false));
    }

    private async Task<CurrencyCode> GetBaseCurrencyAsync(
        CancellationToken cancellationToken) =>
        await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ??
        CurrencyCode.EGP;
}
