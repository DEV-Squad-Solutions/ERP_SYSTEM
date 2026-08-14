using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.StockAdjustments;

public sealed record StockAdjustmentFilterRequest(
    string? DocumentNumber = null,
    int? StoreId = null,
    StockAdjustmentDirection? Direction = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null)
{
    public const int DocumentNumberMaximumLength = 50;
}
