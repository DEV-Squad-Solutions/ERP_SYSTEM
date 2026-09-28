using Microsoft.EntityFrameworkCore;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.Inventory;

public sealed class FiscalYearInventoryCarryForwardServiceTests
{
    [Fact]
    public async Task CarryForward_IsYearScopedAndIdempotentOnReclose()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE ItemMovements
            SET FiscalYearId = 1,
                MovementDate = '2025-01-01',
                QuantityIn = 10,
                QuantityOut = 0,
                UnitCost = 10,
                TotalCost = 100,
                QuantityAfter = 10,
                AverageCostAfter = 10,
                InventoryValueAfter = 100
            WHERE Id = 1;

            INSERT INTO ItemMovements (
                CompanyId, FiscalYearId, StoreId, ItemId, ItemUnitId,
                MovementType, ReferenceId, ReferenceNumber, MovementDate,
                QuantityIn, QuantityOut, CostStatus, PendingCostQuantity,
                UnitCost, TotalCost, QuantityAfter, AverageCostAfter,
                InventoryValueAfter, Description, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES (
                1, 1, 1, 1, 1, 7, 500, 'OUT-2025', '2025-12-31',
                0, 4, 1, 0, 10, 40, 6, 10, 60, NULL,
                'test', '2025-12-31', 'test', 0);
            """);
        var carryForward = database.CreateInventoryCarryForwardService();

        var first = await carryForward.CarryForwardAsync(
            sourceFiscalYearId: 1,
            targetFiscalYearId: 100,
            targetStartDate: new DateOnly(2026, 1, 1),
            sourceFiscalYearName: "2025");

        Assert.True(first.IsSuccess);
        var firstDocument = await database.Context.StockOpeningBalances
            .AsNoTracking()
            .Include(balance => balance.Lines)
            .SingleAsync(balance => balance.FiscalYearId == 100);
        var firstLine = Assert.Single(firstDocument.Lines);
        Assert.Equal(new DateOnly(2026, 1, 1), firstDocument.DocumentDate);
        Assert.Equal(6m, firstLine.Quantity);
        Assert.Equal(10m, firstLine.Price);

        var firstMovement = await database.Context.ItemMovements
            .AsNoTracking()
            .SingleAsync(movement =>
                movement.FiscalYearId == 100 &&
                movement.MovementType == ItemMovementType.OpeningBalance);
        Assert.Equal(6m, firstMovement.QuantityIn);
        Assert.Equal(6m, firstMovement.QuantityAfter);
        Assert.Equal(60m, firstMovement.InventoryValueAfter);

        var stock = database.CreateInventoryStockService();
        var sourceBalance = await stock.GetBalancesAsync(
            storeId: 1,
            itemIds: [1],
            asOfDate: new DateOnly(2025, 12, 31));
        var targetBalance = await stock.GetBalancesAsync(
            storeId: 1,
            itemIds: [1],
            asOfDate: new DateOnly(2026, 1, 1));
        Assert.Equal(6m, sourceBalance[1]);
        Assert.Equal(6m, targetBalance[1]);

        await database.Context.ItemMovements
            .Where(movement =>
                movement.FiscalYearId == 1 &&
                movement.ReferenceNumber == "OUT-2025")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(movement => movement.QuantityOut, 3m)
                .SetProperty(movement => movement.TotalCost, 30m)
                .SetProperty(movement => movement.QuantityAfter, 7m)
                .SetProperty(movement => movement.InventoryValueAfter, 70m));
        database.Context.ChangeTracker.Clear();

        var second = await carryForward.CarryForwardAsync(
            sourceFiscalYearId: 1,
            targetFiscalYearId: 100,
            targetStartDate: new DateOnly(2026, 1, 1),
            sourceFiscalYearName: "2025");

        Assert.True(second.IsSuccess);
        Assert.Equal(
            1,
            await database.Context.StockOpeningBalances.CountAsync(
                balance => balance.FiscalYearId == 100));
        Assert.Equal(
            1,
            await database.Context.StockOpeningBalanceLines.CountAsync(line =>
                line.StockOpeningBalance.FiscalYearId == 100));
        var carriedMovement = await database.Context.ItemMovements
            .AsNoTracking()
            .SingleAsync(movement => movement.FiscalYearId == 100);
        Assert.Equal(7m, carriedMovement.QuantityIn);
        Assert.Equal(7m, carriedMovement.QuantityAfter);

        var refreshedTargetBalance = await stock.GetBalancesAsync(
            storeId: 1,
            itemIds: [1],
            asOfDate: new DateOnly(2026, 12, 31));
        Assert.Equal(7m, refreshedTargetBalance[1]);
    }
}
