using MiniErp.Application.Features.InventoryCostReports;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

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

    [Fact]
    public async Task PendingSummary_IncludesInboundPendingQuantity()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var movement = new ItemMovement
        {
            CompanyId = 1,
            StoreId = 1,
            ItemId = 2,
            ItemUnitId = 1,
            MovementType = ItemMovementType.SalesReturn,
            ReferenceId = 500,
            ReferenceNumber = "RETURN-PENDING-500",
            MovementDate = new DateOnly(2026, 1, 5),
            QuantityIn = 3m,
            QuantityOut = 0m
        };
        movement.ApplyCostSnapshot(
            costStatus: InventoryCostStatus.Pending,
            pendingCostQuantity: 3m,
            unitCost: null,
            totalCost: 0m,
            quantityAfter: 3m,
            averageCostAfter: 0m,
            inventoryValueAfter: 0m);
        database.Context.ItemMovements.Add(movement);
        await database.Context.SaveChangesAsync();

        var result = await database.CreateInventoryCostReportService().GetAsync(
            new() { PageNumber = 1, PageSize = 20 },
            new InventoryCostReportFilterRequest(StoreId: 1, ItemId: 2));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(3m, result.Value.Summary.PendingCostQuantity);
        Assert.Equal(1, result.Value.Summary.PendingMovementCount);
    }

    [Fact]
    public async Task ReportDefaultsToCurrentYearAndUsesCarriedOpeningMovement()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE FiscalYears
            SET Name = '2025', StartDate = '2025-01-01',
                EndDate = '2025-12-31', IsCurrent = 0
            WHERE Id = 1;

            INSERT INTO FiscalYears (
                Id, CompanyId, Name, StartDate, EndDate, Status,
                IsCurrent, RowVersion, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES (
                100, 1, '2026', '2026-01-01', '2026-12-31', 1,
                1, randomblob(8), 'test', '2026-01-01', 'test', 0);

            UPDATE ItemMovements
            SET MovementDate = '2025-12-31', FiscalYearId = 1
            WHERE Id = 1;

            INSERT INTO ItemMovements (
                CompanyId, FiscalYearId, StoreId, ItemId, ItemUnitId,
                MovementType, ReferenceId, ReferenceNumber, MovementDate,
                QuantityIn, QuantityOut, CostStatus, PendingCostQuantity,
                UnitCost, TotalCost, QuantityAfter, AverageCostAfter,
                InventoryValueAfter, Description, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES
                (1, 100, 1, 1, 1, 5, 100, 'OPEN-2026', '2026-01-01',
                 10, 0, 1, 0, 10, 100, 10, 10, 100,
                 'Carried opening balance', 'test', '2026-01-01', 'test', 0),
                (1, 100, 1, 1, 1, 6, 101, 'CURRENT-YEAR-IN',
                 '2026-01-02', 5, 0, 1, 0, 20, 100, 15, 10, 150,
                 NULL, 'test', '2026-01-02', 'test', 0);
            """);
        database.Context.ChangeTracker.Clear();

        var service = database.CreateInventoryCostReportService();
        var current = await service.GetAsync(
            new() { PageNumber = 1, PageSize = 20 },
            new InventoryCostReportFilterRequest(
                StoreId: 1,
                ItemId: 1,
                FromDate: new DateOnly(2026, 1, 2)));
        var previous = await service.GetAsync(
            new() { PageNumber = 1, PageSize = 20 },
            new InventoryCostReportFilterRequest(
                StoreId: 1,
                ItemId: 1,
                FiscalYearId: 1));

        Assert.True(current.IsSuccess, current.Error.Description);
        var currentMovement = Assert.Single(current.Value.Items);
        Assert.Equal(100, current.Value.FiscalYearId);
        Assert.Equal("2026", current.Value.FiscalYearName);
        Assert.Equal(
            ItemMovementType.AdjustmentIncrease,
            currentMovement.MovementType);
        Assert.Equal(new DateOnly(2026, 1, 2), currentMovement.MovementDate);
        Assert.Equal(10m, current.Value.Summary.OpeningQuantity);
        Assert.Equal(15m, current.Value.Summary.ClosingQuantity);
        Assert.True(previous.IsSuccess, previous.Error.Description);
        Assert.Equal(1, previous.Value.FiscalYearId);
        Assert.Equal("2025", previous.Value.FiscalYearName);
        Assert.Equal("OPEN-1", Assert.Single(previous.Value.Items).ReferenceNumber);
    }

    [Fact]
    public async Task ReportRejectsDatesOutsideSelectedFiscalYear()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();

        var result = await database.CreateInventoryCostReportService().GetAsync(
            new() { PageNumber = 1, PageSize = 20 },
            new InventoryCostReportFilterRequest(
                StoreId: 1,
                ItemId: 1,
                FromDate: new DateOnly(2026, 1, 1),
                FiscalYearId: 1));

        Assert.True(result.IsFailure);
        Assert.Equal("FiscalYears.QueryDateOutsideRange", result.Error.Code);
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
