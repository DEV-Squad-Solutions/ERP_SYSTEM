using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ExchangeRates;

public enum ExchangeRateImportItemStatus
{
    Imported = 1,
    Updated = 2,
    Skipped = 3,
    Failed = 4
}

public sealed record ExchangeRateImportItemResponse(
    CurrencyCode Currency,
    CurrencyCode BaseCurrency,
    DateOnly RequestedDate,
    DateOnly? RateDate,
    decimal? Rate,
    ExchangeRateImportItemStatus Status,
    string? Reason);

public sealed record ExchangeRateImportResponse(
    DateOnly RequestedDate,
    string Provider,
    int RequestedCount,
    int ReceivedCount,
    int ImportedCount,
    int UpdatedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<ExchangeRateImportItemResponse> Items);
