using MiniErp.Domain.Enums;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Application.Features.StockOpeningBalances;

public sealed record StockOpeningBalanceLineResponse(
    int Id,
    int CompanyId,
    int StockOpeningBalanceId,
    int ItemId,
    string ItemCode,
    string ItemName,
    int? ItemUnitId,
    string? ItemUnitName,
    int Count,
    decimal Weight,
    decimal Quantity,
    decimal Price,
    decimal Total,
    string? Notes)
{
    public InventoryCostStatus? CostStatus { get; init; }

    public decimal? UnitCost { get; init; }

    public decimal InventoryTotalCost { get; init; }

    public decimal QuantityAfter { get; init; }

    public decimal AverageCostAfter { get; init; }

    public decimal InventoryValueAfter { get; init; }
}

public sealed record StockOpeningBalanceListResponse(
    int Id,
    int CompanyId,
    int FiscalYearId,
    string FiscalYearName,
    int StoreId,
    string StoreName,
    string DocumentNumber,
    DateOnly DocumentDate,
    string? Notes,
    int LineCount,
    byte[] RowVersion,
    IReadOnlyList<StockOpeningBalanceLineResponse> Lines)
{
    public bool IsCarriedForward =>
        StockOpeningBalanceCarryForwardRules.IsCarriedForward(DocumentNumber, Notes);

    public bool IsReadOnly => IsCarriedForward;
}

public sealed record StockOpeningBalanceResponse(
    int Id,
    int CompanyId,
    int FiscalYearId,
    string FiscalYearName,
    int StoreId,
    string StoreName,
    string DocumentNumber,
    DateOnly DocumentDate,
    string? Notes,
    byte[] RowVersion,
    IReadOnlyList<StockOpeningBalanceLineResponse> Lines)
{
    public bool IsCarriedForward =>
        StockOpeningBalanceCarryForwardRules.IsCarriedForward(DocumentNumber, Notes);

    public bool IsReadOnly => IsCarriedForward;
}
