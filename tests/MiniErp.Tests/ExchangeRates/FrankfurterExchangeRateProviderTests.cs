using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Services.ExchangeRates;

namespace MiniErp.Tests.ExchangeRates;

public sealed class FrankfurterExchangeRateProviderTests
{
    [Fact]
    public async Task GetRateAsync_UsesDirectPairAndReturnsDecimalRate()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                date = "2025-07-31",
                @base = "USD",
                quote = "EGP",
                rate = 48.67655m
            })
        });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.frankfurter.dev/")
        };
        var provider = new FrankfurterExchangeRateProvider(
            client,
            Options.Create(new FrankfurterOptions { Provider = "CBE" }),
            NullLogger<FrankfurterExchangeRateProvider>.Instance);

        var result = await provider.GetRateAsync(
            CurrencyCode.USD,
            CurrencyCode.EGP,
            new DateOnly(2025, 7, 31));

        Assert.True(result.IsSuccess);
        Assert.Equal(48.67655m, result.Value.Rate);
        Assert.Equal("USD", result.Value.Currency.ToString());
        Assert.Contains("v2/rate/USD/EGP?providers=CBE&date=2025-07-31", handler.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetRateAsync_NormalizesProviderPrecisionToDomainScale()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                date = "2025-07-31",
                @base = "USD",
                quote = "EGP",
                rate = 48.1234567890126m
            })
        });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.frankfurter.dev/")
        };
        var provider = new FrankfurterExchangeRateProvider(
            client,
            Options.Create(new FrankfurterOptions { Provider = "CBE" }),
            NullLogger<FrankfurterExchangeRateProvider>.Instance);

        var result = await provider.GetRateAsync(
            CurrencyCode.USD,
            CurrencyCode.EGP,
            new DateOnly(2025, 7, 31));

        Assert.True(result.IsSuccess);
        Assert.Equal(48.123456789013m, result.Value.Rate);
    }

    [Fact]
    public async Task GetRateAsync_MapsNotFoundToProviderRateNotFound()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.frankfurter.dev/")
        };
        var provider = new FrankfurterExchangeRateProvider(
            client,
            Options.Create(new FrankfurterOptions()),
            NullLogger<FrankfurterExchangeRateProvider>.Instance);

        var result = await provider.GetRateAsync(
            CurrencyCode.USD,
            CurrencyCode.EGP,
            new DateOnly(2025, 7, 31));

        Assert.True(result.IsFailure);
        Assert.Equal("ExchangeRates.ProviderRateNotFound", result.Error.Code);
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(responder(request));
        }
    }
}
