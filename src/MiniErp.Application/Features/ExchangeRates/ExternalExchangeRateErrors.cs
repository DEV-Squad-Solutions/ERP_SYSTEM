using MiniErp.Application.Common.Results;
using System.Net;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ExchangeRates;

public static class ExternalExchangeRateErrors
{
    public static Error MapStatusCode(
        HttpStatusCode statusCode,
        CurrencyCode currency,
        CurrencyCode baseCurrency,
        DateOnly requestedDate) =>
        statusCode switch
        {
            HttpStatusCode.NotFound => RateNotFound(currency, baseCurrency, requestedDate),
            HttpStatusCode.UnprocessableEntity => UnsupportedCurrency(currency),
            HttpStatusCode.BadRequest => InvalidResponse(),
            _ when statusCode >= HttpStatusCode.InternalServerError => ProviderUnavailable(),
            _ => ProviderUnavailable()
        };

    public static Error ProviderUnavailable() =>
        Error.BadGateway(
            "ExchangeRates.ProviderUnavailable",
            "The external exchange-rate provider is currently unavailable.");

    public static Error ProviderTimeout() =>
        Error.GatewayTimeout(
            "ExchangeRates.ProviderTimeout",
            "The external exchange-rate provider did not respond in time.");

    public static Error InvalidResponse() =>
        Error.BadGateway(
            "ExchangeRates.ProviderInvalidResponse",
            "The external exchange-rate provider returned an invalid response.");

    public static Error RateNotFound(
        CurrencyCode currency,
        CurrencyCode baseCurrency,
        DateOnly requestedDate) =>
        Error.NotFound(
            "ExchangeRates.ProviderRateNotFound",
            $"CBE did not publish a direct {currency}/{baseCurrency} rate for {requestedDate:yyyy-MM-dd}; no cross-rate was synthesized.");

    public static Error UnsupportedCurrency(CurrencyCode currency) =>
        Error.Validation(
            "ExchangeRates.ProviderUnsupportedCurrency",
            $"The CBE provider does not support currency {currency}.");

    public static Error DirectionMismatch(
        CurrencyCode currency,
        CurrencyCode baseCurrency) =>
        Error.BadGateway(
            "ExchangeRates.ProviderDirectionMismatch",
            $"The provider response did not match the requested {currency} to {baseCurrency} direction.");

    public static Error InvalidRate() =>
        Error.BadGateway(
            "ExchangeRates.ProviderInvalidRate",
            "The external provider returned a zero or negative rate.");
}
