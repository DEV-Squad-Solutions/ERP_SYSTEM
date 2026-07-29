namespace MiniErp.Application.Features.InventoryCounts;

public sealed record InventoryCountRequest(
    int StoreId,
    string DocumentNumber,
    DateOnly CountDate,
    string? Notes)
{
    public const int DocumentNumberMaximumLength = 50;

    public const int NotesMaximumLength = 1_000;
}

public sealed record InventoryCountLineUpdateRequest(
    int ItemId,
    decimal? PhysicalQuantity,
    string? Notes);

public sealed record InventoryCountUpdateRequest(
    string? Notes,
    IReadOnlyList<InventoryCountLineUpdateRequest> Lines,
    byte[]? RowVersion);

public sealed record InventoryCountReconcileRequest(
    byte[]? RowVersion,
    IReadOnlyList<InventoryCountIncreaseCostRequest>? IncreaseCosts = null);

public sealed record InventoryCountIncreaseCostRequest(
    int ItemId,
    decimal UnitCost);
