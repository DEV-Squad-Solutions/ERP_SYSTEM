using Microsoft.EntityFrameworkCore;
using MiniErp.Domain.Enums;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Tests.Inventory;

public sealed class FiscalYearInventoryCarryForwardServiceTests
{
    [Fact]
    public async Task Reclose_ReplaysTargetSalesAndConnectedTransferCostsWithPreciseCarryPrice()
    {
        await using var database = await InventoryDocumentTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE ItemMovements SET FiscalYearId = 1, MovementDate = '2025-01-01',
                QuantityIn = 10, QuantityOut = 0, UnitCost = 10, TotalCost = 100,
                QuantityAfter = 10, AverageCostAfter = 10, InventoryValueAfter = 100 WHERE Id = 1;
            """);
        var postingSynchronizer = new RecordingPostingSynchronizer(database.Context);
        var service = database.CreateInventoryCarryForwardService(postingSynchronizer: postingSynchronizer,
            enforceCostingFiscalYearPeriod: true);
        Assert.True((await CarryAsync(service)).IsSuccess);
        var openingId = await database.Context.ItemMovements.Where(movement => movement.FiscalYearId == 100)
            .Select(movement => movement.Id).SingleAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ItemMovements (CompanyId, FiscalYearId, StoreId, ItemId, ItemUnitId,
                MovementType, ReferenceId, ReferenceNumber, MovementDate, QuantityIn, QuantityOut,
                CostStatus, PendingCostQuantity, UnitCost, TotalCost, QuantityAfter, AverageCostAfter,
                InventoryValueAfter, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1,100,1,1,1,1,900,'SALE-2026','2026-02-01',0,2,1,0,10,20,8,10,80,'test','2026-02-01','test',0),
                (1,100,1,1,1,9,901,'TRANSFER-2026','2026-03-01',0,3,1,0,10,30,5,10,50,'test','2026-03-01','test',0),
                (1,100,2,1,1,8,901,'TRANSFER-2026','2026-03-01',3,0,1,0,10,30,3,10,30,'test','2026-03-01','test',0),
                (1,100,2,1,1,1,902,'SALE-DEST','2026-04-01',0,1,1,0,10,10,2,10,20,'test','2026-04-01','test',0);
            UPDATE ItemMovements SET AverageCostAfter = 12.33333333, UnitCost = 12.33333333,
                TotalCost = 123.33333330, InventoryValueAfter = 123.33333330 WHERE Id = 1;
            """);
        database.Context.ChangeTracker.Clear();

        var result = await CarryAsync(service);

        Assert.True(result.IsSuccess);
        var opening = await database.Context.ItemMovements.AsNoTracking().SingleAsync(movement => movement.Id == openingId);
        Assert.Equal(12.33333333m, opening.UnitCost);
        Assert.Equal(123.33333330m, opening.TotalCost);
        Assert.Equal(12.33m, await database.Context.StockOpeningBalanceLines
            .Where(line => line.StockOpeningBalance.FiscalYearId == 100).Select(line => line.Price).SingleAsync());
        var sale = await database.Context.ItemMovements.AsNoTracking().SingleAsync(movement => movement.ReferenceId == 900);
        Assert.Equal(24.66666666m, sale.TotalCost);
        Assert.Equal(8m, sale.QuantityAfter);
        Assert.Equal(98.66666664m, sale.InventoryValueAfter);
        var destinationSale = await database.Context.ItemMovements.AsNoTracking().SingleAsync(movement => movement.ReferenceId == 902);
        Assert.Equal(12.33333333m, destinationSale.UnitCost);
        Assert.Equal(12.33333333m, destinationSale.TotalCost);
        var snapshots = await database.CreateInventoryCostingService().GetSnapshotsAsync(
            storeId: 2, itemIds: [1], asOfDate: new DateOnly(2026, 12, 31));
        Assert.Equal(2m, snapshots[1].Quantity);
        Assert.Equal(12.33333333m, snapshots[1].AverageCost);
        Assert.Equal(24.66666666m, snapshots[1].InventoryValue);
        Assert.Contains(new InventoryCostingKey(StoreId: 2, ItemId: 1, FiscalYearId: 100), postingSynchronizer.Keys);
        Assert.Equal(24.66666666m, postingSynchronizer.PersistedSaleCost);

        // The real current-year write guard still selects 2025. Internal
        // recosting must accept the movement-owned open 2026 period.
        Assert.True(await database.Context.FiscalYears.AnyAsync(year => year.Id == 1 && year.IsCurrent));
        Assert.True(await database.Context.FiscalYears.AnyAsync(year => year.Id == 100 && !year.IsCurrent));
        await database.Context.FiscalYears.Where(year => year.Id == 100)
            .ExecuteUpdateAsync(setters => setters.SetProperty(year => year.Status, FiscalYearStatus.Closed));
        await database.Context.ItemMovements.Where(movement => movement.Id == 1)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(movement => movement.AverageCostAfter, 20m)
                .SetProperty(movement => movement.TotalCost, 200m)
                .SetProperty(movement => movement.InventoryValueAfter, 200m));
        database.Context.ChangeTracker.Clear();

        var closedTarget = await CarryAsync(service);
        Assert.True(closedTarget.IsFailure);
        Assert.Equal("FiscalYears.Closed", closedTarget.Error.Code);
        Assert.Equal(12.33333333m, await database.Context.ItemMovements
            .Where(movement => movement.Id == openingId).Select(movement => movement.UnitCost).SingleAsync());
        Assert.Equal(24.66666666m, await database.Context.ItemMovements
            .Where(movement => movement.ReferenceId == 900).Select(movement => movement.TotalCost).SingleAsync());
    }

    [Fact]
    public async Task Reclose_RemovingCarryReplaysStaleKeysAndRejectsNegativeTargetTimeline()
    {
        await using var database = await InventoryDocumentTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();
        await database.SetStockBalanceCheckModeAsync(StockBalanceCheckMode.Both);
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE ItemMovements SET FiscalYearId = 1, MovementDate = '2025-01-01',
                QuantityIn = 10, QuantityOut = 0, UnitCost = 10, TotalCost = 100,
                QuantityAfter = 10, AverageCostAfter = 10, InventoryValueAfter = 100 WHERE Id = 1;
            """);
        var inventoryPosting = new RecordingInventoryPostingService();
        var service = database.CreateInventoryCarryForwardService(inventoryPostingService: inventoryPosting);
        Assert.True((await CarryAsync(service)).IsSuccess);
        var generatedDocumentId = await database.Context.StockOpeningBalances
            .Where(balance => balance.FiscalYearId == 100).Select(balance => balance.Id).SingleAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ItemMovements (CompanyId, FiscalYearId, StoreId, ItemId, ItemUnitId,
                MovementType, ReferenceId, ReferenceNumber, MovementDate, QuantityIn, QuantityOut,
                CostStatus, PendingCostQuantity, UnitCost, TotalCost, QuantityAfter, AverageCostAfter,
                InventoryValueAfter, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (1,100,1,1,1,1,900,'SALE-2026','2026-02-01',0,2,1,0,10,20,8,10,80,'test','2026-02-01','test',0);
            UPDATE ItemMovements SET QuantityIn = 0, TotalCost = 0, QuantityAfter = 0,
                InventoryValueAfter = 0 WHERE Id = 1;
            """);
        database.Context.ChangeTracker.Clear();

        var rejected = await CarryAsync(service);
        Assert.True(rejected.IsFailure);
        Assert.Empty(inventoryPosting.DeletedSources);
        Assert.Equal(10m, await database.Context.ItemMovements.Where(movement => movement.FiscalYearId == 100 &&
            movement.MovementType == ItemMovementType.OpeningBalance).Select(movement => movement.QuantityIn).SingleAsync());

        await database.Context.ItemMovements.Where(movement => movement.ReferenceId == 900).ExecuteDeleteAsync();
        database.Context.ChangeTracker.Clear();
        Assert.True((await CarryAsync(service)).IsSuccess);
        Assert.False(await database.Context.StockOpeningBalances.AnyAsync(balance => balance.FiscalYearId == 100));
        Assert.Equal((JournalEntrySourceType.StockOpeningBalance, generatedDocumentId),
            Assert.Single(inventoryPosting.DeletedSources));
        var snapshot = await database.CreateInventoryCostingService().GetSnapshotsAsync(
            storeId: 1, itemIds: [1], asOfDate: new DateOnly(2026, 12, 31));
        Assert.Equal(0m, snapshot[1].Quantity);
        Assert.Equal(0m, snapshot[1].InventoryValue);
        Assert.Equal(0m, await database.Context.ItemStoreBalances.Where(balance => balance.CompanyId == 1 &&
            balance.StoreId == 1 && balance.ItemId == 1).Select(balance => balance.Quantity).SingleAsync());
    }

    private static Task<MiniErp.Application.Common.Results.Result> CarryAsync(
        MiniErp.Infrastructure.Services.FiscalYears.FiscalYearInventoryCarryForwardService service) =>
        service.CarryForwardAsync(sourceFiscalYearId: 1, targetFiscalYearId: 100,
            targetStartDate: new DateOnly(2026, 1, 1), sourceFiscalYearName: "2025");

    private sealed class RecordingPostingSynchronizer(ApplicationDbContext context)
        : IInventoryCostPostingSynchronizer
    {
        public IReadOnlyCollection<InventoryCostingKey> Keys { get; private set; } = [];
        public decimal? PersistedSaleCost { get; private set; }

        public async Task<Error?> SynchronizeAsync(IReadOnlyCollection<InventoryCostingKey> keys,
            CancellationToken cancellationToken = default)
        {
            Keys = keys.ToArray();
            PersistedSaleCost = await context.ItemMovements.AsNoTracking()
                .Where(movement => movement.FiscalYearId == 100 && movement.ReferenceId == 900)
                .Select(movement => (decimal?)movement.TotalCost).SingleOrDefaultAsync(cancellationToken);
            return null;
        }
    }

    private sealed class RecordingInventoryPostingService : IInventoryPostingService
    {
        public List<(JournalEntrySourceType SourceType, int SourceId)> DeletedSources { get; } = [];

        public Task<Result> SynchronizeStockOpeningBalanceAsync(int stockOpeningBalanceId,
            CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());

        public Task<Result> SynchronizeStockAdjustmentAsync(int stockAdjustmentId,
            CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());

        public Task<Result> DeleteAsync(JournalEntrySourceType sourceType, int sourceId,
            CancellationToken cancellationToken = default)
        {
            DeletedSources.Add((sourceType, sourceId));
            return Task.FromResult(Result.Success());
        }
    }

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
