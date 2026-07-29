using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.Inventory;

public sealed class InventoryCostingServiceTests
{
    static InventoryCostingServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task Increase_RecalculatesWeightedAverage()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();

        var adjustment = await AddAsync(
            database,
            "COST-IN-1",
            StockAdjustmentDirection.Increase,
            itemId: 1,
            quantity: 10m,
            unitCost: 20m);

        var movement = await MovementAsync(database, adjustment.Id);
        Assert.Equal(20m, movement.QuantityAfter);
        Assert.Equal(10m, movement.AverageCostAfter);
        Assert.Equal(200m, movement.InventoryValueAfter);
        Assert.Equal(200m, movement.TotalCost);
    }

    [Fact]
    public async Task Decrease_UsesCurrentAverageWithoutChangingIt()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await AddAsync(
            database,
            "COST-IN-2",
            StockAdjustmentDirection.Increase,
            1,
            10m,
            20m);

        var decrease = await AddAsync(
            database,
            "COST-OUT-2",
            StockAdjustmentDirection.Decrease,
            1,
            4m);

        var movement = await MovementAsync(database, decrease.Id);
        Assert.Equal(10m, movement.UnitCost);
        Assert.Equal(40m, movement.TotalCost);
        Assert.Equal(16m, movement.QuantityAfter);
        Assert.Equal(10m, movement.AverageCostAfter);
        Assert.Equal(160m, movement.InventoryValueAfter);
    }

    [Fact]
    public async Task DecreaseToZero_ResetsAverageAndValue()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await AddAsync(
            database,
            "COST-IN-3",
            StockAdjustmentDirection.Increase,
            1,
            10m,
            20m);

        var decrease = await AddAsync(
            database,
            "COST-OUT-3",
            StockAdjustmentDirection.Decrease,
            1,
            20m);

        var movement = await MovementAsync(database, decrease.Id);
        Assert.Equal(0m, movement.QuantityAfter);
        Assert.Equal(0m, movement.AverageCostAfter);
        Assert.Equal(0m, movement.InventoryValueAfter);
    }

    [Fact]
    public async Task PartiallyCoveredOutbound_BecomesPartiallyCosted()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(
            StockBalanceCheckMode.None);

        var decrease = await AddAsync(
            database,
            "COST-PARTIAL-4",
            StockAdjustmentDirection.Decrease,
            1,
            12m);

        var movement = await MovementAsync(database, decrease.Id);
        Assert.Equal(InventoryCostStatus.PartiallyCosted, movement.CostStatus);
        Assert.Equal(2m, movement.PendingCostQuantity);
        Assert.Null(movement.UnitCost);
        Assert.Equal(-2m, movement.QuantityAfter);
        Assert.Equal(0m, movement.AverageCostAfter);
    }

    [Fact]
    public async Task UncoveredOutbound_BecomesPending()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(
            StockBalanceCheckMode.None);

        var decrease = await AddAsync(
            database,
            "COST-PENDING-5",
            StockAdjustmentDirection.Decrease,
            2,
            10m);

        var movement = await MovementAsync(database, decrease.Id);
        Assert.Equal(InventoryCostStatus.Pending, movement.CostStatus);
        Assert.Equal(10m, movement.PendingCostQuantity);
        Assert.Null(movement.UnitCost);
        Assert.Equal(0m, movement.TotalCost);
    }

    [Fact]
    public async Task FutureInbound_PartiallyCoversPendingOutbound()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(
            StockBalanceCheckMode.None);
        var decrease = await AddAsync(
            database,
            "COST-PENDING-6",
            StockAdjustmentDirection.Decrease,
            2,
            10m,
            date: new DateOnly(2026, 7, 10));

        await AddAsync(
            database,
            "COST-COVER-6",
            StockAdjustmentDirection.Increase,
            2,
            4m,
            5m,
            new DateOnly(2026, 7, 11));

        var movement = await MovementAsync(database, decrease.Id);
        Assert.Equal(InventoryCostStatus.PartiallyCosted, movement.CostStatus);
        Assert.Equal(6m, movement.PendingCostQuantity);
        Assert.Null(movement.UnitCost);
        Assert.Equal(20m, movement.TotalCost);
    }

    [Fact]
    public async Task MultipleFutureInbounds_FullyRevaluePendingOutbound()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(
            StockBalanceCheckMode.None);
        var decrease = await AddAsync(
            database,
            "COST-PENDING-7",
            StockAdjustmentDirection.Decrease,
            2,
            10m,
            date: new DateOnly(2026, 7, 10));
        await AddAsync(
            database,
            "COST-COVER-A-7",
            StockAdjustmentDirection.Increase,
            2,
            4m,
            5m,
            new DateOnly(2026, 7, 11));
        await AddAsync(
            database,
            "COST-COVER-B-7",
            StockAdjustmentDirection.Increase,
            2,
            6m,
            7m,
            new DateOnly(2026, 7, 12));

        var movement = await MovementAsync(database, decrease.Id);
        Assert.Equal(InventoryCostStatus.Revalued, movement.CostStatus);
        Assert.Equal(0m, movement.PendingCostQuantity);
        Assert.Equal(6.2m, movement.UnitCost);
        Assert.Equal(62m, movement.TotalCost);
        Assert.Equal(
            2,
            await database.Context.InventoryCostAllocations.CountAsync());
    }

    [Fact]
    public async Task InboundExcessAfterCoverage_StartsPositiveStockAtInboundCost()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(
            StockBalanceCheckMode.None);
        var decrease = await AddAsync(
            database,
            "COST-PENDING-8",
            StockAdjustmentDirection.Decrease,
            2,
            10m,
            date: new DateOnly(2026, 7, 10));

        var increase = await AddAsync(
            database,
            "COST-COVER-8",
            StockAdjustmentDirection.Increase,
            2,
            12m,
            5m,
            new DateOnly(2026, 7, 11));

        var outbound = await MovementAsync(database, decrease.Id);
        var inbound = await MovementAsync(database, increase.Id);
        Assert.Equal(InventoryCostStatus.Revalued, outbound.CostStatus);
        Assert.Equal(50m, outbound.TotalCost);
        Assert.Equal(2m, inbound.QuantityAfter);
        Assert.Equal(5m, inbound.AverageCostAfter);
        Assert.Equal(10m, inbound.InventoryValueAfter);
    }

    [Fact]
    public async Task PendingOutbounds_AreCoveredInFifoOrder()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(
            StockBalanceCheckMode.None);
        var first = await AddAsync(
            database,
            "COST-FIFO-A-9",
            StockAdjustmentDirection.Decrease,
            2,
            3m,
            date: new DateOnly(2026, 7, 10));
        var second = await AddAsync(
            database,
            "COST-FIFO-B-9",
            StockAdjustmentDirection.Decrease,
            2,
            4m,
            date: new DateOnly(2026, 7, 11));
        await AddAsync(
            database,
            "COST-FIFO-IN-9",
            StockAdjustmentDirection.Increase,
            2,
            5m,
            10m,
            new DateOnly(2026, 7, 12));

        var firstMovement = await MovementAsync(database, first.Id);
        var secondMovement = await MovementAsync(database, second.Id);
        Assert.Equal(InventoryCostStatus.Revalued, firstMovement.CostStatus);
        Assert.Equal(0m, firstMovement.PendingCostQuantity);
        Assert.Equal(
            InventoryCostStatus.PartiallyCosted,
            secondMovement.CostStatus);
        Assert.Equal(2m, secondMovement.PendingCostQuantity);
        Assert.Equal(20m, secondMovement.TotalCost);
    }

    [Fact]
    public async Task MultipleInboundAllocations_ProduceWeightedOutboundUnitCost()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(
            StockBalanceCheckMode.None);
        var decrease = await AddAsync(
            database,
            "COST-MULTI-10",
            StockAdjustmentDirection.Decrease,
            2,
            10m,
            date: new DateOnly(2026, 7, 10));
        await AddAsync(
            database,
            "COST-MULTI-A-10",
            StockAdjustmentDirection.Increase,
            2,
            4m,
            2m,
            new DateOnly(2026, 7, 11));
        await AddAsync(
            database,
            "COST-MULTI-B-10",
            StockAdjustmentDirection.Increase,
            2,
            6m,
            4m,
            new DateOnly(2026, 7, 12));

        var movement = await MovementAsync(database, decrease.Id);
        Assert.Equal(32m, movement.TotalCost);
        Assert.Equal(3.2m, movement.UnitCost);
    }

    [Fact]
    public async Task BackdatedCreate_ReplaysAllSubsequentMovements()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await AddAsync(
            database,
            "COST-BACK-A-11",
            StockAdjustmentDirection.Increase,
            2,
            10m,
            10m,
            new DateOnly(2026, 7, 10));
        var decrease = await AddAsync(
            database,
            "COST-BACK-OUT-11",
            StockAdjustmentDirection.Decrease,
            2,
            5m,
            date: new DateOnly(2026, 7, 20));
        Assert.Equal(
            10m,
            (await MovementAsync(database, decrease.Id)).UnitCost);

        await AddAsync(
            database,
            "COST-BACK-B-11",
            StockAdjustmentDirection.Increase,
            2,
            10m,
            30m,
            new DateOnly(2026, 7, 15));

        Assert.Equal(
            20m,
            (await MovementAsync(database, decrease.Id)).UnitCost);
    }

    [Fact]
    public async Task CostEdit_ReplaysTimelineAndPreservesMovementIdentity()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateStockAdjustmentService();
        var increase = await AddAsync(
            database,
            "COST-EDIT-12",
            StockAdjustmentDirection.Increase,
            2,
            10m,
            10m,
            new DateOnly(2026, 7, 10));
        var movementBefore = await MovementAsync(database, increase.Id);
        var decrease = await AddAsync(
            database,
            "COST-EDIT-OUT-12",
            StockAdjustmentDirection.Decrease,
            2,
            5m,
            date: new DateOnly(2026, 7, 20));

        var updated = await service.UpdateAsync(
            increase.Id,
            new StockAdjustmentUpdateRequest(
                increase.StoreId,
                increase.DocumentNumber,
                increase.DocumentDate,
                increase.Direction,
                increase.Reason,
                [
                    new StockAdjustmentLineRequest(2, 10m, null)
                    {
                        UnitCost = 20m
                    }
                ],
                increase.RowVersion));

        Assert.True(updated.IsSuccess, updated.Error.Description);
        var movementAfter = await MovementAsync(database, increase.Id);
        Assert.Equal(movementBefore.Id, movementAfter.Id);
        Assert.Equal(movementBefore.CreatedOn, movementAfter.CreatedOn);
        Assert.Equal(
            20m,
            (await MovementAsync(database, decrease.Id)).UnitCost);
    }

    [Fact]
    public async Task DateEdit_ReconsidersPendingAllocations()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(
            StockBalanceCheckMode.None);
        var service = database.CreateStockAdjustmentService();
        var increase = await AddAsync(
            database,
            "COST-DATE-IN-13",
            StockAdjustmentDirection.Increase,
            2,
            10m,
            10m,
            new DateOnly(2026, 7, 20));
        var decrease = await AddAsync(
            database,
            "COST-DATE-OUT-13",
            StockAdjustmentDirection.Decrease,
            2,
            5m,
            date: new DateOnly(2026, 7, 15));
        Assert.Equal(
            InventoryCostStatus.Revalued,
            (await MovementAsync(database, decrease.Id)).CostStatus);

        var updated = await service.UpdateAsync(
            increase.Id,
            new StockAdjustmentUpdateRequest(
                increase.StoreId,
                increase.DocumentNumber,
                new DateOnly(2026, 7, 10),
                increase.Direction,
                increase.Reason,
                [
                    new StockAdjustmentLineRequest(2, 10m, null)
                    {
                        UnitCost = 10m
                    }
                ],
                increase.RowVersion));

        Assert.True(updated.IsSuccess, updated.Error.Description);
        var outbound = await MovementAsync(database, decrease.Id);
        Assert.Equal(InventoryCostStatus.Final, outbound.CostStatus);
        Assert.Empty(await database.Context.InventoryCostAllocations
            .ToListAsync());
    }

    [Fact]
    public async Task DeleteInbound_ReplaysOutboundAsPending()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(
            StockBalanceCheckMode.None);
        var service = database.CreateStockAdjustmentService();
        var increase = await AddAsync(
            database,
            "COST-DELETE-IN-14",
            StockAdjustmentDirection.Increase,
            2,
            10m,
            10m,
            new DateOnly(2026, 7, 10));
        var decrease = await AddAsync(
            database,
            "COST-DELETE-OUT-14",
            StockAdjustmentDirection.Decrease,
            2,
            5m,
            date: new DateOnly(2026, 7, 20));

        var deleted = await service.DeleteAsync(increase.Id);

        Assert.True(deleted.IsSuccess, deleted.Error.Description);
        var movement = await MovementAsync(database, decrease.Id);
        Assert.Equal(InventoryCostStatus.Pending, movement.CostStatus);
        Assert.Equal(5m, movement.PendingCostQuantity);
        Assert.Null(movement.UnitCost);
    }

    [Fact]
    public async Task Recalculation_RebuildsOneAllocationPerMovementPair()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(
            StockBalanceCheckMode.None);
        var service = database.CreateStockAdjustmentService();
        await AddAsync(
            database,
            "COST-PAIR-OUT-15",
            StockAdjustmentDirection.Decrease,
            2,
            10m,
            date: new DateOnly(2026, 7, 10));
        var increase = await AddAsync(
            database,
            "COST-PAIR-IN-15",
            StockAdjustmentDirection.Increase,
            2,
            10m,
            5m,
            new DateOnly(2026, 7, 11));

        var updated = await service.UpdateAsync(
            increase.Id,
            new StockAdjustmentUpdateRequest(
                increase.StoreId,
                increase.DocumentNumber,
                increase.DocumentDate,
                increase.Direction,
                increase.Reason,
                [
                    new StockAdjustmentLineRequest(2, 10m, null)
                    {
                        UnitCost = 6m
                    }
                ],
                increase.RowVersion));

        Assert.True(updated.IsSuccess, updated.Error.Description);
        var allocation = Assert.Single(
            await database.Context.InventoryCostAllocations.ToListAsync());
        Assert.Equal(10m, allocation.Quantity);
        Assert.Equal(6m, allocation.UnitCost);
        Assert.Equal(60m, allocation.TotalCost);
    }

    [Fact]
    public async Task CurrentBalance_EqualsLastSnapshotAndAccountingEquation()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await AddAsync(
            database,
            "COST-EQ-IN-A-16",
            StockAdjustmentDirection.Increase,
            2,
            10m,
            10m,
            new DateOnly(2026, 7, 10));
        await AddAsync(
            database,
            "COST-EQ-IN-B-16",
            StockAdjustmentDirection.Increase,
            2,
            10m,
            20m,
            new DateOnly(2026, 7, 11));
        var decrease = await AddAsync(
            database,
            "COST-EQ-OUT-16",
            StockAdjustmentDirection.Decrease,
            2,
            4m,
            date: new DateOnly(2026, 7, 12));

        var last = await MovementAsync(database, decrease.Id);
        var balance = await database.Context.ItemStoreBalances
            .AsNoTracking()
            .SingleAsync(itemStoreBalance =>
                itemStoreBalance.CompanyId == 1 &&
                itemStoreBalance.StoreId == 1 &&
                itemStoreBalance.ItemId == 2);
        var inboundCost = await database.Context.ItemMovements
            .Where(movement =>
                movement.CompanyId == 1 &&
                movement.StoreId == 1 &&
                movement.ItemId == 2 &&
                movement.QuantityIn > 0m)
            .SumAsync(movement => movement.TotalCost);
        var outboundCost = await database.Context.ItemMovements
            .Where(movement =>
                movement.CompanyId == 1 &&
                movement.StoreId == 1 &&
                movement.ItemId == 2 &&
                movement.QuantityOut > 0m)
            .SumAsync(movement => movement.TotalCost);

        Assert.Equal(last.QuantityAfter, balance.Quantity);
        Assert.Equal(last.AverageCostAfter, balance.AverageCost);
        Assert.Equal(last.InventoryValueAfter, balance.InventoryValue);
        Assert.Equal(
            balance.InventoryValue,
            inboundCost - outboundCost);
    }

    private static async Task<StockAdjustmentResponse> AddAsync(
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
                documentNumber,
                date ?? new DateOnly(2026, 7, 28),
                direction,
                null,
                [
                    new StockAdjustmentLineRequest(itemId, quantity, null)
                    {
                        UnitCost = unitCost
                    }
                ]));

        Assert.True(result.IsSuccess, result.Error.Description);
        database.Context.ChangeTracker.Clear();
        return result.Value;
    }

    private static Task<ItemMovement> MovementAsync(
        InventoryDocumentTestDatabase database,
        int adjustmentId) =>
        database.Context.ItemMovements
            .AsNoTracking()
            .SingleAsync(movement =>
                movement.CompanyId == 1 &&
                movement.ReferenceId == adjustmentId &&
                movement.MovementType != ItemMovementType.OpeningBalance);
}
