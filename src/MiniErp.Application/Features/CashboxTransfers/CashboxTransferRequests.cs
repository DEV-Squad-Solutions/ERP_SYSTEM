namespace MiniErp.Application.Features.CashboxTransfers;

public sealed record CashboxTransferRequest(
    DateOnly TransferDate,
    int SourceCashboxId,
    int DestinationCashboxId,
    decimal Amount,
    string? Description,
    string? Notes,
    decimal? ExchangeRate = null,
    decimal? DestinationAmount = null,
    decimal? ConversionRate = null)
{
    public const int DescriptionMaximumLength = 1_000;

    public const int NotesMaximumLength = 1_000;
}

public sealed record CashboxTransferUpdateRequest(
    DateOnly TransferDate,
    int SourceCashboxId,
    int DestinationCashboxId,
    decimal Amount,
    string? Description,
    string? Notes,
    byte[]? RowVersion,
    decimal? ExchangeRate = null,
    decimal? DestinationAmount = null,
    decimal? ConversionRate = null);

public sealed record CashboxTransferFilterRequest(
    string? Search = null,
    int? SourceCashboxId = null,
    int? DestinationCashboxId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);
