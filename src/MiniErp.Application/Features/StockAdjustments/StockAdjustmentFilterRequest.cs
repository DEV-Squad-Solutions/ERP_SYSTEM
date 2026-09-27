using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.StockAdjustments;

public sealed record StockAdjustmentFilterRequest(
    int? FiscalYearId = null,
    string? DocumentNumber = null,
    int? StoreId = null,
    StockAdjustmentDirection? Direction = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null)
{
    public const int DocumentNumberMaximumLength = 50;
}
