using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.StockAdjustments;

public sealed record StockAdjustmentLineRequest(
    int ItemId,
    decimal Quantity,
    string? Reason)
{
    public decimal? UnitCost { get; init; }
}

public sealed record StockAdjustmentRequest(
    int StoreId,
    string DocumentNumber,
    DateOnly DocumentDate,
    StockAdjustmentDirection Direction,
    string? Reason,
    IReadOnlyList<StockAdjustmentLineRequest> Lines)
{
    public const int DocumentNumberMaximumLength = 50;

    public const int ReasonMaximumLength = 1_000;

    public const int MaximumLineCount = 100;
}

public sealed record StockAdjustmentUpdateRequest(
    int StoreId,
    string DocumentNumber,
    DateOnly DocumentDate,
    StockAdjustmentDirection Direction,
    string? Reason,
    IReadOnlyList<StockAdjustmentLineRequest> Lines,
    byte[]? RowVersion);
