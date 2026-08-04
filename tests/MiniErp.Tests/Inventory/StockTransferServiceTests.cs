using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Application.Features.StockTransfers;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.Inventory;

public sealed class StockTransferServiceTests
{
    static StockTransferServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task Create_UsesSourceAverageCostForDestinationAndCreatesPairedMovements()
    {
        await using var database = await InventoryDocumentTestDatabase.CreateAsync();
        await AddSourceCostAsync(database, "SOURCE-COST", 10m, 20m);

        var result = await database.CreateStockTransferService().AddAsync(
            Request("TR-1", 4m));

        Assert.True(result.IsSuccess, result.Error.Description);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(10m, line.SourceUnitCost);
        Assert.Equal(10m, line.DestinationUnitCost);
        Assert.Equal(40m, line.SourceTotalCost);
        Assert.Equal(40m, line.DestinationTotalCost);
        Assert.Equal(16m, line.SourceQuantityAfter);
        Assert.Equal(4m, line.DestinationQuantityAfter);
        Assert.Equal(10m, line.DestinationAverageCostAfter);
        Assert.Equal(40m, line.DestinationInventoryValueAfter);

        var movements = await database.Context.ItemMovements
            .AsNoTracking()
            .Where(movement => movement.ReferenceNumber == "TR-1")
            .OrderBy(movement => movement.MovementType)
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Contains(movements, movement =>
            movement.MovementType == ItemMovementType.TransferOut &&
            movement.StoreId == 1 && movement.QuantityOut == 4m);
        Assert.Contains(movements, movement =>
            movement.MovementType == ItemMovementType.TransferIn &&
            movement.StoreId == 4 && movement.QuantityIn == 4m);
    }

    [Fact]
    public async Task Create_RejectsQuantityAboveSourceHistoricalBalance()
    {
        await using var database = await InventoryDocumentTestDatabase.CreateAsync();

        var result = await database.CreateStockTransferService().AddAsync(
            Request("TR-TOO-MUCH", 11m));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);
        Assert.Empty(await database.Context.StockTransfers.ToListAsync());
        Assert.Empty(await database.Context.ItemMovements
            .Where(movement => movement.MovementType == ItemMovementType.TransferOut ||
                movement.MovementType == ItemMovementType.TransferIn)
            .ToListAsync());
    }

    [Fact]
    public async Task Create_WithoutACompleteSourceCost_UsesZeroCostWhenBalanceCheckIsDisabled()
    {
        await using var database = await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(StockBalanceCheckMode.None);

        var result = await database.CreateStockTransferService().AddAsync(
            Request("TR-NO-COST", 11m));

        Assert.True(result.IsSuccess, result.Error.Description);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(0m, line.DestinationUnitCost);
    }

    [Fact]
    public async Task Create_RecalculatesWeightedAverageInDestination()
    {
        await using var database = await InventoryDocumentTestDatabase.CreateAsync();
        await AddSourceCostAsync(database, "SOURCE-COST", 10m, 20m);
        await database.CreateStockAdjustmentService().AddAsync(
            new StockAdjustmentRequest(
                StoreId: 4,
                DocumentNumber: "DEST-COST",
                DocumentDate: new DateOnly(2026, 7, 1),
                Direction: StockAdjustmentDirection.Increase,
                Reason: null,
                Lines:
                [
                    new StockAdjustmentLineRequest(
                        ItemId: 1,
                        Quantity: 10m,
                        Reason: null)
                    {
                        UnitCost = 30m
                    }
                ]));

        var result = await database.CreateStockTransferService().AddAsync(
            Request("TR-WEIGHTED", 10m));

        Assert.True(result.IsSuccess, result.Error.Description);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(20m, line.DestinationQuantityAfter);
        Assert.Equal(20m, line.DestinationAverageCostAfter);
        Assert.Equal(400m, line.DestinationInventoryValueAfter);
    }

    [Fact]
    public async Task Update_PreservesMovementIdsAdvancesVersionAndRejectsStaleVersion()
    {
        await using var database = await InventoryDocumentTestDatabase.CreateAsync();
        await AddSourceCostAsync(database, "SOURCE-COST", 10m, 20m);
        var service = database.CreateStockTransferService();
        var created = (await service.AddAsync(Request("TR-UPDATE", 2m))).Value;
        var originalLine = Assert.Single(created.Lines);

        var updated = await service.UpdateAsync(
            created.Id,
            new StockTransferUpdateRequest(
                TransferDate: new DateOnly(2026, 7, 3),
                Notes: "updated",
                Lines: [new StockTransferLineRequest(1, 3m, "line")],
                RowVersion: created.RowVersion));

        Assert.True(updated.IsSuccess, updated.Error.Description);
        var updatedLine = Assert.Single(updated.Value.Lines);
        Assert.Equal(originalLine.SourceMovementId, updatedLine.SourceMovementId);
        Assert.Equal(
            originalLine.DestinationMovementId,
            updatedLine.DestinationMovementId);
        Assert.Equal(3m, updatedLine.Quantity);
        Assert.False(created.RowVersion.SequenceEqual(updated.Value.RowVersion));

        var stale = await service.UpdateAsync(
            created.Id,
            new StockTransferUpdateRequest(
                TransferDate: created.TransferDate,
                Notes: null,
                Lines: [new StockTransferLineRequest(1, 4m, null)],
                RowVersion: created.RowVersion));
        Assert.True(stale.IsFailure);
        Assert.Equal("StockTransfers.Concurrency", stale.Error.Code);
    }

    [Fact]
    public async Task Delete_IsBlockedWhenDestinationReceiptSupportsLaterOutbound()
    {
        await using var database = await InventoryDocumentTestDatabase.CreateAsync();
        await AddSourceCostAsync(database, "SOURCE-COST", 10m, 20m);
        var transferService = database.CreateStockTransferService();
        var transfer = (await transferService.AddAsync(
            Request("TR-DELETE", 5m))).Value;
        var outbound = await database.CreateStockAdjustmentService().AddAsync(
            new StockAdjustmentRequest(
                StoreId: 4,
                DocumentNumber: "DEST-OUT",
                DocumentDate: new DateOnly(2026, 7, 5),
                Direction: StockAdjustmentDirection.Decrease,
                Reason: null,
                Lines: [new StockAdjustmentLineRequest(1, 5m, null)]));
        Assert.True(outbound.IsSuccess, outbound.Error.Description);

        var result = await transferService.DeleteAsync(transfer.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.HistoricalStockConflict", result.Error.Code);
        Assert.NotNull(await database.Context.StockTransfers
            .AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == transfer.Id));
    }

    [Fact]
    public async Task BackdatedSourceCostChange_PropagatesToDestinationTransferCost()
    {
        await using var database = await InventoryDocumentTestDatabase.CreateAsync();
        await AddSourceCostAsync(database, "SOURCE-COST", 10m, 20m);
        var transfer = (await database.CreateStockTransferService().AddAsync(
            Request("TR-PROPAGATE", 5m))).Value;
        var before = Assert.Single(transfer.Lines);
        Assert.Equal(10m, before.DestinationUnitCost);

        var backdated = await database.CreateStockAdjustmentService().AddAsync(
            new StockAdjustmentRequest(
                StoreId: 1,
                DocumentNumber: "BACKDATED-COST",
                DocumentDate: new DateOnly(2026, 7, 1),
                Direction: StockAdjustmentDirection.Increase,
                Reason: null,
                Lines:
                [
                    new StockAdjustmentLineRequest(1, 10m, null)
                    {
                        UnitCost = 30m
                    }
                ]));
        Assert.True(backdated.IsSuccess, backdated.Error.Description);

        var refreshed = await database.CreateStockTransferService()
            .GetByIdAsync(transfer.Id);
        Assert.True(refreshed.IsSuccess, refreshed.Error.Description);
        var after = Assert.Single(refreshed.Value.Lines);
        Assert.Equal(after.SourceUnitCost, after.DestinationUnitCost);
        Assert.Equal(16.66666667m, after.DestinationUnitCost);
        Assert.Equal(83.33333335m, after.DestinationInventoryValueAfter);
    }

    [Fact]
    public async Task GetById_DoesNotExposeAnotherCompanyTransfer()
    {
        await using var database = await InventoryDocumentTestDatabase.CreateAsync();
        await AddSourceCostAsync(database, "SOURCE-COST", 10m, 20m);
        var transfer = (await database.CreateStockTransferService().AddAsync(
            Request("TR-TENANT", 1m))).Value;

        var result = await database.CreateStockTransferService(companyId: 2)
            .GetByIdAsync(transfer.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("StockTransfers.NotFound", result.Error.Code);
    }

    private static StockTransferRequest Request(
        string documentNumber,
        decimal quantity) =>
        new(
            DocumentNumber: documentNumber,
            TransferDate: new DateOnly(2026, 7, 2),
            SourceStoreId: 1,
            DestinationStoreId: 4,
            Notes: null,
            Lines: [new StockTransferLineRequest(1, quantity, null)]);

    private static async Task AddSourceCostAsync(
        InventoryDocumentTestDatabase database,
        string documentNumber,
        decimal quantity,
        decimal unitCost)
    {
        var result = await database.CreateStockAdjustmentService().AddAsync(
            new StockAdjustmentRequest(
                StoreId: 1,
                DocumentNumber: documentNumber,
                DocumentDate: new DateOnly(2026, 7, 1),
                Direction: StockAdjustmentDirection.Increase,
                Reason: null,
                Lines:
                [
                    new StockAdjustmentLineRequest(1, quantity, null)
                    {
                        UnitCost = unitCost
                    }
                ]));
        Assert.True(result.IsSuccess, result.Error.Description);
    }
}
