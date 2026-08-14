using MiniErp.Application.Features.InventoryCostReports;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.Inventory;

public sealed class InventoryCostReportServiceTests
{
    [Fact]
    public async Task ReportIncludesTimelineSnapshotsAndOpeningClosingBalances()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();

        await AddAdjustmentAsync(
            database,
            "REPORT-IN-1",
            StockAdjustmentDirection.Increase,
            itemId: 1,
            quantity: 10m,
            unitCost: 20m,
            date: new DateOnly(2026, 1, 2));
        await AddAdjustmentAsync(
            database,
            "REPORT-OUT-1",
            StockAdjustmentDirection.Decrease,
            itemId: 1,
            quantity: 5m,
            date: new DateOnly(2026, 1, 3));

        var result = await database.CreateInventoryCostReportService().GetAsync(
            new() { PageNumber = 1, PageSize = 20 },
            new InventoryCostReportFilterRequest(
                StoreId: 1,
                ItemId: 1,
                FromDate: new DateOnly(2026, 1, 2),
                ToDate: new DateOnly(2026, 1, 3)));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal("Piece", result.Value.ItemUnitName);
        Assert.Equal(10m, result.Value.Summary.OpeningQuantity);
        Assert.Equal(10m, result.Value.Summary.TotalQuantityIn);
        Assert.Equal(5m, result.Value.Summary.TotalQuantityOut);
        Assert.Equal(200m, result.Value.Summary.TotalInboundCost);
        Assert.Equal(50m, result.Value.Summary.TotalOutboundCost);
        Assert.Equal(15m, result.Value.Summary.ClosingQuantity);
        Assert.Equal(10m, result.Value.Summary.ClosingAverageCost);
        Assert.Equal(150m, result.Value.Summary.ClosingInventoryValue);
        Assert.Equal(15m, result.Value.Summary.CurrentQuantity);
        Assert.Equal(10m, result.Value.Summary.CurrentAverageCost);
        Assert.Equal(150m, result.Value.Summary.CurrentInventoryValue);
        Assert.Equal(20m, result.Value.Items[0].QuantityAfter);
        Assert.Equal(10m, result.Value.Items[0].AverageCostAfter);
        Assert.Equal(15m, result.Value.Items[1].QuantityAfter);
        Assert.Equal(10m, result.Value.Items[1].AverageCostAfter);
    }

    [Fact]
    public async Task ReportIncludesRevaluationAllocationsForBothSides()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(StockBalanceCheckMode.None);

        await AddAdjustmentAsync(
            database,
            "REPORT-PENDING-2",
            StockAdjustmentDirection.Decrease,
            itemId: 2,
            quantity: 10m,
            date: new DateOnly(2026, 1, 2));
        await AddAdjustmentAsync(
            database,
            "REPORT-COVER-2",
            StockAdjustmentDirection.Increase,
            itemId: 2,
            quantity: 10m,
            unitCost: 7m,
            date: new DateOnly(2026, 1, 3));

        var result = await database.CreateInventoryCostReportService().GetAsync(
            new() { PageNumber = 1, PageSize = 20 },
            new InventoryCostReportFilterRequest(StoreId: 1, ItemId: 2));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(InventoryCostStatus.Revalued, result.Value.Items[0].CostStatus);
        Assert.Equal(7m, result.Value.Items[0].UnitCost);
        Assert.Contains(
            result.Value.Items[0].Allocations,
            allocation => !allocation.IsInboundAllocation &&
                allocation.RelatedMovementId == result.Value.Items[1].MovementId);
        Assert.Contains(
            result.Value.Items[1].Allocations,
            allocation => allocation.IsInboundAllocation &&
                allocation.RelatedMovementId == result.Value.Items[0].MovementId);
        Assert.Equal(1, result.Value.Summary.RevaluedMovementCount);
        Assert.Equal(0m, result.Value.Summary.PendingCostQuantity);
    }

    private static async Task AddAdjustmentAsync(
        InventoryDocumentTestDatabase database,
        string documentNumber,
        StockAdjustmentDirection direction,
        int itemId,
        decimal quantity,
        decimal? unitCost = null,
        DateOnly? date = null)
    {
        var result = await database.CreateStockAdjustmentService().AddAsync(
            new StockAdjustmentRequest(
                1,
                date ?? new DateOnly(2026, 1, 2),
                direction,
                null,
                [
                    new StockAdjustmentLineRequest(itemId, quantity, null)
                    {
                        UnitCost = unitCost
                    }
                ]));

        Assert.True(result.IsSuccess, result.Error.Description);
    }
}
