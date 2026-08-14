using System.Globalization;
using static MiniErp.Application.Features.ExchangeRates.ExternalExchangeRateErrors;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.ExchangeRates;

public sealed class FrankfurterExchangeRateProvider(
    HttpClient httpClient,
    IOptions<FrankfurterOptions> options,
    ILogger<FrankfurterExchangeRateProvider> logger)
    : IExchangeRateProvider
{
    private readonly FrankfurterOptions options = options.Value;

    public string Name => $"Frankfurter:{options.Provider}";

    public async Task<Result<ExternalExchangeRate>> GetRateAsync(
        CurrencyCode currency,
        CurrencyCode baseCurrency,
        DateOnly requestedDate,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(currency) || !Enum.IsDefined(baseCurrency))
        {
            return Result<ExternalExchangeRate>.Failure(
                UnsupportedCurrency(currency));
        }

        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"v2/rate/{currency}/{baseCurrency}?providers={Uri.EscapeDataString(options.Provider)}&date={requestedDate:yyyy-MM-dd}");

        try
        {
            using var response = await httpClient.GetAsync(
                path,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Frankfurter rate request failed with status {StatusCode} for {Currency}/{BaseCurrency} on {RequestedDate}.",
                    (int)response.StatusCode,
                    currency,
                    baseCurrency,
                    requestedDate);
                return Result<ExternalExchangeRate>.Failure(
                    MapStatusCode(response.StatusCode, currency, baseCurrency, requestedDate));
            }

            FrankfurterRateResponse? providerResponse;
            try
            {
                providerResponse = await response.Content
                    .ReadFromJsonAsync<FrankfurterRateResponse>(
                        cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Result<ExternalExchangeRate>.Failure(ProviderTimeout());
            }
            catch (Exception exception) when (
                exception is System.Text.Json.JsonException or
                NotSupportedException)
            {
                logger.LogWarning(
                    exception,
                    "Frankfurter returned invalid JSON for {Currency}/{BaseCurrency} on {RequestedDate}.",
                    currency,
                    baseCurrency,
                    requestedDate);
                return Result<ExternalExchangeRate>.Failure(InvalidResponse());
            }

            if (providerResponse is null || providerResponse.Date == default)
            {
                return Result<ExternalExchangeRate>.Failure(InvalidResponse());
            }

            if (!string.Equals(
                    providerResponse.Base,
                    currency.ToString(),
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    providerResponse.Quote,
                    baseCurrency.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return Result<ExternalExchangeRate>.Failure(
                    DirectionMismatch(currency, baseCurrency));
            }

            var normalizedRate = ExchangeRateRules.RoundRate(providerResponse.Rate);
            if (providerResponse.Rate <= 0m ||
                !ExchangeRateRules.IsValidRate(normalizedRate))
            {
                return Result<ExternalExchangeRate>.Failure(InvalidRate());
            }

            return Result<ExternalExchangeRate>.Success(
                new ExternalExchangeRate(
                    currency,
                    baseCurrency,
                    requestedDate,
                    providerResponse.Date,
                    normalizedRate,
                    Name));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<ExternalExchangeRate>.Failure(ProviderTimeout());
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Frankfurter is unavailable for {Currency}/{BaseCurrency} on {RequestedDate}.",
                currency,
                baseCurrency,
                requestedDate);
            return Result<ExternalExchangeRate>.Failure(ProviderUnavailable());
        }
    }

    internal sealed record FrankfurterRateResponse(
        DateOnly Date,
        string Base,
        string Quote,
        decimal Rate);
}
