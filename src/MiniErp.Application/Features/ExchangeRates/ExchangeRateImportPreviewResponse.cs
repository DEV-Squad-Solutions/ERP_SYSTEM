using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ExchangeRates;

public sealed record ExchangeRateImportPreviewItemResponse(
    CurrencyCode Currency,
    CurrencyCode BaseCurrency,
    DateOnly RequestedDate,
    DateOnly? RateDate,
    decimal? Rate,
    string? Error);

public sealed record ExchangeRateImportPreviewResponse(
    DateOnly RequestedDate,
    string Provider,
    CurrencyCode BaseCurrency,
    int RequestedCount,
    int ReceivedCount,
    IReadOnlyList<ExchangeRateImportPreviewItemResponse> Items);
