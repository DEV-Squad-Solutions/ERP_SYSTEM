using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.InventoryCounts;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.Inventory;

public sealed class InventoryDocumentServiceTests
{
    static InventoryDocumentServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task StockAdjustment_CreateAndLineOnlyUpdate_ReplaceMovementAndAdvanceVersion()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateStockAdjustmentService();

        var created = await service.AddAsync(
            AdjustmentRequest(
                "SA-1",
                StockAdjustmentDirection.Increase,
                2m));

        Assert.True(created.IsSuccess);
        var initialVersion = created.Value.RowVersion;
        var initialMovement = await database.Context.ItemMovements
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(ItemMovementType.AdjustmentIncrease, initialMovement.MovementType);
        Assert.Equal(2m, initialMovement.QuantityIn);
        Assert.Equal(0m, initialMovement.QuantityOut);

        var updated = await service.UpdateAsync(
            created.Value.Id,
            new StockAdjustmentUpdateRequest(
                1,
                "SA-1",
                new DateOnly(2026, 7, 28),
                StockAdjustmentDirection.Increase,
                "line-only change",
                [new StockAdjustmentLineRequest(1, 3m, "count correction")],
                initialVersion));

        Assert.True(updated.IsSuccess);
        Assert.False(initialVersion.SequenceEqual(updated.Value.RowVersion));
        Assert.Equal(3m, updated.Value.Lines.Single().Quantity);
        var currentMovement = await database.Context.ItemMovements
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(3m, currentMovement.QuantityIn);

        var staleUpdate = await service.UpdateAsync(
            created.Value.Id,
            new StockAdjustmentUpdateRequest(
                1,
                "SA-1",
                new DateOnly(2026, 7, 28),
                StockAdjustmentDirection.Increase,
                null,
                [new StockAdjustmentLineRequest(1, 4m, null)],
                initialVersion));

        Assert.Equal("StockAdjustments.Concurrency", staleUpdate.Error.Code);
    }

    [Fact]
    public async Task StockAdjustment_DecreaseRejectsInsufficientStock()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateStockAdjustmentService();

        var result = await service.AddAsync(
            AdjustmentRequest(
                "SA-OUT",
                StockAdjustmentDirection.Decrease,
                11m));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);
        Assert.Empty(await database.Context.StockAdjustments.ToListAsync());
        Assert.Empty(await database.Context.ItemMovements.ToListAsync());
    }

    [Fact]
    public async Task StockAdjustment_DecreaseUsesAvailableStockAndCreatesOutboundMovement()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateStockAdjustmentService();

        var result = await service.AddAsync(
            AdjustmentRequest(
                "SA-OUT-VALID",
                StockAdjustmentDirection.Decrease,
                4m));

        Assert.True(result.IsSuccess);
        var movement = Assert.Single(
            await database.Context.ItemMovements.AsNoTracking().ToListAsync());
        Assert.Equal(ItemMovementType.AdjustmentDecrease, movement.MovementType);
        Assert.Equal(4m, movement.QuantityOut);
        Assert.Equal(0m, movement.QuantityIn);
    }

    [Fact]
    public async Task StockAdjustment_HeaderOnlyUpdate_AdvancesVersionAndRejectsStaleHeader()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateStockAdjustmentService();
        var created = (await service.AddAsync(
            AdjustmentRequest("SA-HEADER", StockAdjustmentDirection.Increase, 2m))).Value;

        var updated = await service.UpdateAsync(
            created.Id,
            new StockAdjustmentUpdateRequest(
                created.StoreId,
                created.DocumentNumber,
                created.DocumentDate,
                created.Direction,
                "header-only change",
                [new StockAdjustmentLineRequest(1, 2m, null)],
                created.RowVersion));

        Assert.True(updated.IsSuccess);
        Assert.False(created.RowVersion.SequenceEqual(updated.Value.RowVersion));
        Assert.Equal("header-only change", updated.Value.Reason);

        var stale = await service.UpdateAsync(
            created.Id,
            new StockAdjustmentUpdateRequest(
                created.StoreId,
                created.DocumentNumber,
                created.DocumentDate,
                created.Direction,
                "stale header change",
                [new StockAdjustmentLineRequest(1, 2m, null)],
                created.RowVersion));

        Assert.True(stale.IsFailure);
        Assert.Equal("StockAdjustments.Concurrency", stale.Error.Code);
    }

    [Fact]
    public async Task StockAdjustment_UpdateReplacesAddedAndRemovedLinesAtomically()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateStockAdjustmentService();
        var created = (await service.AddAsync(
            new StockAdjustmentRequest(
                1,
                "SA-LINES",
                new DateOnly(2026, 7, 28),
                StockAdjustmentDirection.Increase,
                null,
                [
                    new StockAdjustmentLineRequest(1, 2m, "old line"),
                    new StockAdjustmentLineRequest(2, 4m, null)
                ]))).Value;

        var updated = await service.UpdateAsync(
            created.Id,
            new StockAdjustmentUpdateRequest(
                1,
                "SA-LINES",
                new DateOnly(2026, 7, 28),
                StockAdjustmentDirection.Increase,
                null,
                [new StockAdjustmentLineRequest(2, 5m, "new line")],
                created.RowVersion));

        Assert.True(updated.IsSuccess);
        Assert.False(created.RowVersion.SequenceEqual(updated.Value.RowVersion));
        var line = Assert.Single(updated.Value.Lines);
        Assert.Equal(2, line.ItemId);
        Assert.Equal(5m, line.Quantity);
        Assert.Equal("new line", line.Reason);
        var movements = await database.Context.ItemMovements
            .AsNoTracking()
            .ToListAsync();
        var movement = Assert.Single(movements);
        Assert.Equal(2, movement.ItemId);
        Assert.Equal(5m, movement.QuantityIn);
    }

    [Fact]
    public async Task StockAdjustment_PaginatedItemsIncludeOrderedCompleteLines()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateStockAdjustmentService();

        await service.AddAsync(
            new StockAdjustmentRequest(
                1,
                "SA-LIST",
                new DateOnly(2026, 7, 28),
                StockAdjustmentDirection.Increase,
                null,
                [
                    new StockAdjustmentLineRequest(2, 4m, null),
                    new StockAdjustmentLineRequest(1, 2m, null)
                ]));

        var page = await service.GetAllAsync(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20
            });

        Assert.True(page.IsSuccess);
        var item = Assert.Single(page.Value.Items);
        Assert.Equal(2, item.LineCount);
        Assert.Equal([1, 2], item.Lines.Select(line => line.ItemId));
    }

    [Fact]
    public async Task StockAdjustment_DeleteSoftDeletesAggregateAndTouchesHeader()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateStockAdjustmentService();
        var created = (await service.AddAsync(
            AdjustmentRequest("SA-DELETE", StockAdjustmentDirection.Increase, 2m))).Value;

        var deleted = await service.DeleteAsync(created.Id);

        Assert.True(deleted.IsSuccess);
        var missing = await service.GetByIdAsync(created.Id);
        Assert.True(missing.IsFailure);
        Assert.Equal("StockAdjustments.NotFound", missing.Error.Code);

        var stored = await database.Context.StockAdjustments
            .IgnoreQueryFilters()
            .SingleAsync(adjustment => adjustment.Id == created.Id);
        var storedLine = await database.Context.StockAdjustmentLines
            .IgnoreQueryFilters()
            .SingleAsync(line => line.StockAdjustmentId == created.Id);
        Assert.True(stored.IsDeleted);
        Assert.True(storedLine.IsDeleted);
        Assert.True(stored.LastModifiedAt > created.LastModifiedAt);
        Assert.Empty(await database.Context.ItemMovements.ToListAsync());
    }

    [Fact]
    public async Task StockAdjustment_IsTenantSafe()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var companyOneService = database.CreateStockAdjustmentService(1);
        var companyTwoService = database.CreateStockAdjustmentService(2);
        var created = await companyOneService.AddAsync(
            AdjustmentRequest(
                "SA-TENANT",
                StockAdjustmentDirection.Increase,
                1m));

        var read = await companyTwoService.GetByIdAsync(created.Value.Id);
        var add = await companyTwoService.AddAsync(
            AdjustmentRequest(
                "SA-OTHER",
                StockAdjustmentDirection.Increase,
                1m));

        Assert.Equal("StockAdjustments.NotFound", read.Error.Code);
        Assert.Equal("StockAdjustments.StoreNotFound", add.Error.Code);
    }

    [Fact]
    public async Task InventoryCount_CreateIncludesEveryActiveItemAndZeroBalances()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateInventoryCountService();

        var result = await service.AddAsync(CountRequest("COUNT-1"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Lines.Count);
        Assert.Equal(
            10m,
            result.Value.Lines.Single(line => line.ItemId == 1).SystemQuantity);
        Assert.Equal(
            0m,
            result.Value.Lines.Single(line => line.ItemId == 2).SystemQuantity);
        Assert.All(
            result.Value.Lines,
            line => Assert.Null(line.PhysicalQuantity));
    }

    [Fact]
    public async Task InventoryCount_ReconcileCreatesOnlyRequiredInAndOutAdjustments()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var countService = database.CreateInventoryCountService();
        var adjustmentService = database.CreateStockAdjustmentService();
        var created = (await countService.AddAsync(
            CountRequest("COUNT-MIXED"))).Value;
        var updated = await UpdateCountAsync(
            countService,
            created,
            physicalByItem: new Dictionary<int, decimal?>
            {
                [1] = 8m,
                [2] = 3m
            });
        database.Context.ChangeTracker.Clear();

        var reconciled = await countService.ReconcileAsync(
            created.Id,
            new InventoryCountReconcileRequest(updated.RowVersion));

        Assert.True(reconciled.IsSuccess);
        Assert.NotNull(reconciled.Value.ReconciledAt);
        Assert.NotNull(reconciled.Value.IncreaseAdjustmentId);
        Assert.NotNull(reconciled.Value.DecreaseAdjustmentId);

        var adjustments = await database.Context.StockAdjustments
            .AsNoTracking()
            .Include(adjustment => adjustment.Lines)
            .OrderBy(adjustment => adjustment.Direction)
            .ToListAsync();
        Assert.Equal(2, adjustments.Count);

        var increase = adjustments.Single(adjustment =>
            adjustment.Direction == StockAdjustmentDirection.Increase);
        var decrease = adjustments.Single(adjustment =>
            adjustment.Direction == StockAdjustmentDirection.Decrease);
        Assert.Equal(3m, increase.Lines.Single().Quantity);
        Assert.Equal(2m, decrease.Lines.Single().Quantity);
        Assert.All(
            adjustments,
            adjustment => Assert.Equal(created.Id, adjustment.SourceInventoryCountId));

        var movements = await database.Context.ItemMovements
            .AsNoTracking()
            .OrderBy(movement => movement.MovementType)
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Contains(
            movements,
            movement =>
                movement.MovementType == ItemMovementType.AdjustmentIncrease &&
                movement.ItemId == 2 &&
                movement.QuantityIn == 3m);
        Assert.Contains(
            movements,
            movement =>
                movement.MovementType == ItemMovementType.AdjustmentDecrease &&
                movement.ItemId == 1 &&
                movement.QuantityOut == 2m);

        var generatedUpdate = await adjustmentService.UpdateAsync(
            increase.Id,
            new StockAdjustmentUpdateRequest(
                increase.StoreId,
                increase.DocumentNumber,
                increase.DocumentDate,
                increase.Direction,
                increase.Reason,
                increase.Lines.Select(line =>
                    new StockAdjustmentLineRequest(
                        line.ItemId,
                        line.Quantity,
                        line.Reason)).ToArray(),
                reconciled.Value.IncreaseAdjustmentId == increase.Id
                    ? (await adjustmentService.GetByIdAsync(increase.Id))
                        .Value.RowVersion
                    : []));

        Assert.Equal(
            "StockAdjustments.GeneratedAdjustmentImmutable",
            generatedUpdate.Error.Code);
    }

    [Fact]
    public async Task InventoryCount_NoDifferencesCreatesNoAdjustments()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateInventoryCountService();
        var created = (await service.AddAsync(
            CountRequest("COUNT-EQUAL"))).Value;
        var updated = await UpdateCountAsync(
            service,
            created,
            physicalByItem: new Dictionary<int, decimal?>
            {
                [1] = 10m,
                [2] = 0m
            });
        database.Context.ChangeTracker.Clear();

        var reconciled = await service.ReconcileAsync(
            created.Id,
            new InventoryCountReconcileRequest(updated.RowVersion));

        Assert.True(reconciled.IsSuccess);
        Assert.Null(reconciled.Value.IncreaseAdjustmentId);
        Assert.Null(reconciled.Value.DecreaseAdjustmentId);
        Assert.Empty(await database.Context.StockAdjustments.ToListAsync());
        Assert.Empty(await database.Context.ItemMovements.ToListAsync());
    }

    [Fact]
    public async Task InventoryCount_ReconcileRejectsMovementAfterSnapshot()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateInventoryCountService();
        var created = (await service.AddAsync(
            CountRequest("COUNT-STALE"))).Value;

        database.Context.ItemMovements.Add(
            new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.AdjustmentIncrease,
                ReferenceId = 999,
                ReferenceNumber = "LATE",
                MovementDate = new DateOnly(2026, 8, 1),
                QuantityIn = 1m,
                QuantityOut = 0m
            });
        await database.Context.SaveChangesAsync();

        var updated = await UpdateCountAsync(
            service,
            created,
            physicalByItem: new Dictionary<int, decimal?>
            {
                [1] = 10m,
                [2] = 0m
            });
        database.Context.ChangeTracker.Clear();
        var result = await service.ReconcileAsync(
            created.Id,
            new InventoryCountReconcileRequest(updated.RowVersion));

        Assert.True(result.IsFailure);
        Assert.Equal("InventoryCounts.SnapshotStale", result.Error.Code);
        Assert.Empty(await database.Context.StockAdjustments.ToListAsync());
    }

    [Fact]
    public async Task InventoryCount_UpdateRequiresCompleteFrozenItemSet()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateInventoryCountService();
        var created = (await service.AddAsync(
            CountRequest("COUNT-FROZEN"))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            new InventoryCountUpdateRequest(
                null,
                [new InventoryCountLineUpdateRequest(1, 10m, null)],
                created.RowVersion));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "InventoryCounts.LinesDoNotMatchSnapshot",
            result.Error.Code);
    }

    private static StockAdjustmentRequest AdjustmentRequest(
        string documentNumber,
        StockAdjustmentDirection direction,
        decimal quantity) =>
        new(
            1,
            documentNumber,
            new DateOnly(2026, 7, 28),
            direction,
            null,
            [new StockAdjustmentLineRequest(1, quantity, null)]);

    private static InventoryCountRequest CountRequest(string documentNumber) =>
        new(
            1,
            documentNumber,
            new DateOnly(2026, 7, 28),
            null);

    private static async Task<InventoryCountResponse> UpdateCountAsync(
        MiniErp.Infrastructure.Services.InventoryCounts.InventoryCountService service,
        InventoryCountResponse count,
        IReadOnlyDictionary<int, decimal?> physicalByItem)
    {
        var result = await service.UpdateAsync(
            count.Id,
            new InventoryCountUpdateRequest(
                count.Notes,
                count.Lines
                    .Select(line => new InventoryCountLineUpdateRequest(
                        line.ItemId,
                        physicalByItem[line.ItemId],
                        null))
                    .ToArray(),
                count.RowVersion));

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
