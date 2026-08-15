namespace MiniErp.Application.Features.InventoryCounts;

public sealed record InventoryCountFilterRequest(
    string? DocumentNumber = null,
    int? StoreId = null,
    bool? IsReconciled = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null)
{
    public const int DocumentNumberMaximumLength = 50;
}
