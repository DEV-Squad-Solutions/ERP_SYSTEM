using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.InventoryCounts;
using MiniErp.Application.Features.InventoryStockReports;
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
    public async Task StockAdjustment_CreateAndLineOnlyUpdate_PreserveMovementAndAdvanceVersion()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateStockAdjustmentService();

        var created = await service.AddAsync(
            AdjustmentRequest(
                StockAdjustmentDirection.Increase,
                2m));

        Assert.True(created.IsSuccess);
        Assert.Matches(
            "^ADJ-[0-9]{4,}$",
            created.Value.DocumentNumber);
        var initialVersion = created.Value.RowVersion;
        var initialMovement = await database.Context.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.MovementType != ItemMovementType.OpeningBalance)
            .SingleAsync();
        Assert.Equal(ItemMovementType.AdjustmentIncrease, initialMovement.MovementType);
        Assert.Equal(created.Value.DocumentNumber, initialMovement.ReferenceNumber);
        Assert.Equal(2m, initialMovement.QuantityIn);
        Assert.Equal(0m, initialMovement.QuantityOut);

        var updated = await service.UpdateAsync(
            created.Value.Id,
            new StockAdjustmentUpdateRequest(
                1,
                new DateOnly(2026, 7, 28),
                StockAdjustmentDirection.Increase,
                "line-only change",
                [AdjustmentLine(1, 3m, "count correction")],
                initialVersion));

        Assert.True(updated.IsSuccess);
        Assert.False(initialVersion.SequenceEqual(updated.Value.RowVersion));
        Assert.Equal(3m, updated.Value.Lines.Single().Quantity);
        var currentMovement = await database.Context.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.MovementType != ItemMovementType.OpeningBalance)
            .SingleAsync();
        Assert.Equal(initialMovement.Id, currentMovement.Id);
        Assert.Equal(initialMovement.CreatedOn, currentMovement.CreatedOn);
        Assert.Equal(3m, currentMovement.QuantityIn);

        var staleUpdate = await service.UpdateAsync(
            created.Value.Id,
            new StockAdjustmentUpdateRequest(
                1,
                new DateOnly(2026, 7, 28),
                StockAdjustmentDirection.Increase,
                null,
                [AdjustmentLine(1, 4m, null)],
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
                StockAdjustmentDirection.Decrease,
                11m));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);
        Assert.Empty(await database.Context.StockAdjustments.ToListAsync());
        Assert.Empty(await database.Context.ItemMovements
            .Where(movement =>
                movement.MovementType != ItemMovementType.OpeningBalance)
            .ToListAsync());
    }

    [Fact]
    public async Task StockAdjustment_IncreaseRequiresEnteredUnitCost()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var result = await database.CreateStockAdjustmentService().AddAsync(
            new StockAdjustmentRequest(
                1,
                new DateOnly(2026, 7, 28),
                StockAdjustmentDirection.Increase,
                null,
                [new StockAdjustmentLineRequest(1, 2m, null)]));

        Assert.True(result.IsFailure);
        Assert.Equal("StockAdjustments.UnitCostRequired", result.Error.Code);
        Assert.Empty(await database.Context.StockAdjustments.ToListAsync());
    }

    [Fact]
    public async Task StockAdjustment_DecreaseRejectsClientUnitCost()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var result = await database.CreateStockAdjustmentService().AddAsync(
            new StockAdjustmentRequest(
                1,
                new DateOnly(2026, 7, 28),
                StockAdjustmentDirection.Decrease,
                null,
                [
                    new StockAdjustmentLineRequest(1, 2m, null)
                    {
                        UnitCost = 99m
                    }
                ]));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "StockAdjustments.UnitCostNotAllowed",
            result.Error.Code);
        Assert.Empty(await database.Context.StockAdjustments.ToListAsync());
    }

    [Fact]
    public async Task StockAdjustment_NoneModeSkipsOnlyBalanceValidation()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.SetStockBalanceCheckModeAsync(StockBalanceCheckMode.None);
        var service = database.CreateStockAdjustmentService();

        var result = await service.AddAsync(
            AdjustmentRequest(
                StockAdjustmentDirection.Decrease,
                11m));

        Assert.True(result.IsSuccess);
        Assert.Single(await database.Context.ItemMovements
            .Where(movement =>
                movement.MovementType != ItemMovementType.OpeningBalance)
            .ToListAsync());
    }

    [Fact]
    public async Task StockAdjustment_DecreaseUsesAvailableStockAndCreatesOutboundMovement()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateStockAdjustmentService();

        var result = await service.AddAsync(
            AdjustmentRequest(
                StockAdjustmentDirection.Decrease,
                4m));

        Assert.True(result.IsSuccess);
        var movement = Assert.Single(
            await database.Context.ItemMovements
                .AsNoTracking()
                .Where(candidate =>
                    candidate.MovementType !=
                    ItemMovementType.OpeningBalance)
                .ToListAsync());
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
            AdjustmentRequest(StockAdjustmentDirection.Increase, 2m))).Value;

        var updated = await service.UpdateAsync(
            created.Id,
            new StockAdjustmentUpdateRequest(
                created.StoreId,
                created.DocumentDate,
                created.Direction,
                "header-only change",
                [AdjustmentLine(1, 2m, null)],
                created.RowVersion));

        Assert.True(updated.IsSuccess);
        Assert.False(created.RowVersion.SequenceEqual(updated.Value.RowVersion));
        Assert.Equal("header-only change", updated.Value.Reason);

        var stale = await service.UpdateAsync(
            created.Id,
            new StockAdjustmentUpdateRequest(
                created.StoreId,
                created.DocumentDate,
                created.Direction,
                "stale header change",
                [AdjustmentLine(1, 2m, null)],
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
                new DateOnly(2026, 7, 28),
                StockAdjustmentDirection.Increase,
                null,
                [
                    AdjustmentLine(1, 2m, "old line"),
                    AdjustmentLine(2, 4m, null)
                ]))).Value;

        var updated = await service.UpdateAsync(
            created.Id,
            new StockAdjustmentUpdateRequest(
                1,
                new DateOnly(2026, 7, 28),
                StockAdjustmentDirection.Increase,
                null,
                [AdjustmentLine(2, 5m, "new line")],
                created.RowVersion));

        Assert.True(updated.IsSuccess);
        Assert.False(created.RowVersion.SequenceEqual(updated.Value.RowVersion));
        var line = Assert.Single(updated.Value.Lines);
        Assert.Equal(2, line.ItemId);
        Assert.Equal(5m, line.Quantity);
        Assert.Equal("new line", line.Reason);
        var movements = await database.Context.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.MovementType != ItemMovementType.OpeningBalance)
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
                new DateOnly(2026, 7, 28),
                StockAdjustmentDirection.Increase,
                null,
                [
                    AdjustmentLine(2, 4m, null),
                    AdjustmentLine(1, 2m, null)
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
    public async Task StockAdjustment_GetAllDefaultsToCurrentFiscalYear()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO StockAdjustments (
                CompanyId, FiscalYearId, StoreId, DocumentNumber,
                DocumentDate, Direction, Reason, SourceInventoryCountId,
                LastModifiedAt, CreatedById, CreatedOn, CreatedByPc,
                IsDeleted)
            VALUES
                (1, 1, 1, 'ADJ-2025', '2025-06-01', 1, NULL, NULL,
                 '2025-06-01', 'test', '2025-06-01', 'test', 0),
                (1, 100, 1, 'ADJ-2026', '2026-06-01', 1, NULL, NULL,
                 '2026-06-01', 'test', '2026-06-01', 'test', 0);
            """);
        var service = database.CreateStockAdjustmentService();

        var currentYear = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 });
        var nextYear = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new StockAdjustmentFilterRequest(FiscalYearId: 100));
        var invalidDate = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new StockAdjustmentFilterRequest(
                FiscalYearId: 1,
                FromDate: new DateOnly(2026, 1, 1)));

        Assert.True(currentYear.IsSuccess);
        var currentItem = Assert.Single(currentYear.Value.Items);
        Assert.Equal("ADJ-2025", currentItem.DocumentNumber);
        Assert.Equal(1, currentItem.FiscalYearId);
        Assert.Equal("2025", currentItem.FiscalYearName);

        Assert.True(nextYear.IsSuccess);
        var nextItem = Assert.Single(nextYear.Value.Items);
        Assert.Equal("ADJ-2026", nextItem.DocumentNumber);
        Assert.Equal(100, nextItem.FiscalYearId);
        Assert.Equal("2026", nextItem.FiscalYearName);
        Assert.Equal(
            "StockAdjustments.NotFound",
            (await service.GetByIdAsync(nextItem.Id)).Error.Code);
        Assert.Equal(
            100,
            (await service.GetByIdAsync(nextItem.Id, fiscalYearId: 100))
                .Value.FiscalYearId);
        Assert.Equal(
            "FiscalYears.QueryDateOutsideRange",
            invalidDate.Error.Code);
    }

    [Fact]
    public async Task StockAdjustment_DeleteSoftDeletesAggregateAndTouchesHeader()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateStockAdjustmentService();
        var created = (await service.AddAsync(
            AdjustmentRequest(StockAdjustmentDirection.Increase, 2m))).Value;

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
        Assert.Empty(await database.Context.ItemMovements
            .Where(movement =>
                movement.MovementType != ItemMovementType.OpeningBalance)
            .ToListAsync());
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
                StockAdjustmentDirection.Increase,
                1m));

        var read = await companyTwoService.GetByIdAsync(created.Value.Id);
        var add = await companyTwoService.AddAsync(
            AdjustmentRequest(
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

        var result = await service.AddAsync(CountRequest());

        Assert.True(result.IsSuccess);
        Assert.Matches(
            "^IC-[0-9]{4,}$",
            result.Value.DocumentNumber);
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
    public async Task InventoryCount_GetAllDefaultsToCurrentFiscalYear()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO InventoryCounts (
                CompanyId, FiscalYearId, StoreId, DocumentNumber,
                CountDate, SnapshotTakenAt, ReconciledAt, Notes,
                LastModifiedAt, CreatedById, CreatedOn, CreatedByPc,
                IsDeleted)
            VALUES
                (1, 1, 1, 'IC-2025', '2025-06-01',
                 '2025-06-01', NULL, NULL, '2025-06-01',
                 'test', '2025-06-01', 'test', 0),
                (1, 100, 1, 'IC-2026', '2026-06-01',
                 '2026-06-01', NULL, NULL, '2026-06-01',
                 'test', '2026-06-01', 'test', 0);
            """);
        var service = database.CreateInventoryCountService();

        var currentYear = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 });
        var nextYear = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new InventoryCountFilterRequest(FiscalYearId: 100));
        var invalidDate = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new InventoryCountFilterRequest(
                FiscalYearId: 1,
                FromDate: new DateOnly(2026, 1, 1)));

        Assert.True(currentYear.IsSuccess);
        var currentItem = Assert.Single(currentYear.Value.Items);
        Assert.Equal("IC-2025", currentItem.DocumentNumber);
        Assert.Equal(1, currentItem.FiscalYearId);
        Assert.Equal("2025", currentItem.FiscalYearName);

        Assert.True(nextYear.IsSuccess);
        var nextItem = Assert.Single(nextYear.Value.Items);
        Assert.Equal("IC-2026", nextItem.DocumentNumber);
        Assert.Equal(100, nextItem.FiscalYearId);
        Assert.Equal("2026", nextItem.FiscalYearName);
        Assert.Equal(
            "InventoryCounts.NotFound",
            (await service.GetByIdAsync(nextItem.Id)).Error.Code);
        Assert.Equal(
            100,
            (await service.GetByIdAsync(nextItem.Id, fiscalYearId: 100))
                .Value.FiscalYearId);
        Assert.Equal(
            "FiscalYears.QueryDateOutsideRange",
            invalidDate.Error.Code);
    }

    [Fact]
    public async Task ClosedFiscalYearBlocksAdjustmentAndInventoryCountMutations()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var adjustmentService = database.CreateStockAdjustmentService();
        var countService = database.CreateInventoryCountService();
        var adjustment = (await adjustmentService.AddAsync(
            AdjustmentRequest(
                StockAdjustmentDirection.Increase,
                2m))).Value;
        var count = (await countService.AddAsync(CountRequest())).Value;
        await database.Context.FiscalYears
            .Where(year => year.Id == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                year => year.Status,
                FiscalYearStatus.Closed));
        database.Context.ChangeTracker.Clear();

        var adjustmentUpdate = await adjustmentService.UpdateAsync(
            adjustment.Id,
            new StockAdjustmentUpdateRequest(
                StoreId: adjustment.StoreId,
                DocumentDate: adjustment.DocumentDate,
                Direction: adjustment.Direction,
                Reason: "closed",
                Lines: [AdjustmentLine(1, 2m, null)],
                RowVersion: adjustment.RowVersion));
        var adjustmentDelete = await adjustmentService.DeleteAsync(
            adjustment.Id);
        var countUpdate = await countService.UpdateAsync(
            count.Id,
            new InventoryCountUpdateRequest(
                Notes: "closed",
                Lines: count.Lines.Select(line =>
                    new InventoryCountLineUpdateRequest(
                        ItemId: line.ItemId,
                        PhysicalQuantity: line.SystemQuantity,
                        Notes: null)).ToArray(),
                RowVersion: count.RowVersion));
        var countReconcile = await countService.ReconcileAsync(
            count.Id,
            new InventoryCountReconcileRequest(count.RowVersion));
        var countDelete = await countService.DeleteAsync(
            count.Id,
            count.RowVersion);

        Assert.Equal("FiscalYears.Closed", adjustmentUpdate.Error.Code);
        Assert.Equal("FiscalYears.Closed", adjustmentDelete.Error.Code);
        Assert.Equal("FiscalYears.Closed", countUpdate.Error.Code);
        Assert.Equal("FiscalYears.Closed", countReconcile.Error.Code);
        Assert.Equal("FiscalYears.Closed", countDelete.Error.Code);
        Assert.Single(await database.Context.StockAdjustments
            .Where(item => item.Id == adjustment.Id)
            .ToListAsync());
        Assert.Single(await database.Context.InventoryCounts
            .Where(item => item.Id == count.Id)
            .ToListAsync());
    }

    [Fact]
    public async Task InventoryStockReport_UsesOnlySelectedFiscalYearMovements()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ItemMovements (
                CompanyId, FiscalYearId, StoreId, ItemId, ItemUnitId,
                MovementType, ReferenceId, ReferenceNumber, MovementDate,
                QuantityIn, QuantityOut, CostStatus, PendingCostQuantity,
                UnitCost, TotalCost, QuantityAfter, AverageCostAfter,
                InventoryValueAfter, Description, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 1, 1, 1, 1, 501, 'FY-2025', '2025-05-01',
                 5, 0, 1, 0, 2, 10, 5, 2, 10, NULL,
                 'test', '2025-05-01', 'test', 0),
                (1, 100, 1, 1, 1, 1, 601, 'FY-2026', '2026-05-01',
                 11, 0, 1, 0, 3, 33, 11, 3, 33, NULL,
                 'test', '2026-05-01', 'test', 0);
            """);
        var service = database.CreateInventoryStockReportService();
        var pagination = new PaginationRequest
        {
            PageNumber = 1,
            PageSize = 20
        };

        var currentYear = await service.GetAsync(
            pagination,
            new InventoryStockReportFilterRequest(
                StoreId: 1,
                AsOfDate: new DateOnly(2025, 12, 31),
                ItemId: 1));
        var nextYear = await service.GetAsync(
            pagination,
            new InventoryStockReportFilterRequest(
                StoreId: 1,
                AsOfDate: new DateOnly(2026, 12, 31),
                ItemId: 1,
                FiscalYearId: 100));

        Assert.True(currentYear.IsSuccess);
        Assert.Equal(1, currentYear.Value.FiscalYearId);
        Assert.Equal("2025", currentYear.Value.FiscalYearName);
        Assert.Equal(5m, Assert.Single(currentYear.Value.Items).Balance);

        Assert.True(nextYear.IsSuccess);
        Assert.Equal(100, nextYear.Value.FiscalYearId);
        Assert.Equal("2026", nextYear.Value.FiscalYearName);
        Assert.Equal(11m, Assert.Single(nextYear.Value.Items).Balance);
    }

    [Fact]
    public async Task InventoryStockReport_RejectsAsOfDateOutsideSelectedFiscalYear()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();
        var service = database.CreateInventoryStockReportService();

        var result = await service.GetAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new InventoryStockReportFilterRequest(
                StoreId: 1,
                AsOfDate: new DateOnly(2026, 1, 1),
                FiscalYearId: 1));

        Assert.True(result.IsFailure);
        Assert.Equal("FiscalYears.QueryDateOutsideRange", result.Error.Code);
    }

    [Fact]
    public async Task InventoryCount_ReconcileCreatesOnlyRequiredInAndOutAdjustments()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var countService = database.CreateInventoryCountService();
        var adjustmentService = database.CreateStockAdjustmentService();
        var created = (await countService.AddAsync(
            CountRequest())).Value;
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
            new InventoryCountReconcileRequest(
                updated.RowVersion,
                [new InventoryCountIncreaseCostRequest(2, 4m)]));

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
        Assert.Equal("ADJ-0001", increase.DocumentNumber);
        Assert.Equal("ADJ-0002", decrease.DocumentNumber);
        Assert.Equal(3m, increase.Lines.Single().Quantity);
        Assert.Equal(4m, increase.Lines.Single().UnitCost);
        Assert.Equal(2m, decrease.Lines.Single().Quantity);
        Assert.Null(decrease.Lines.Single().UnitCost);
        Assert.All(
            adjustments,
            adjustment => Assert.Equal(created.Id, adjustment.SourceInventoryCountId));

        var movements = await database.Context.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.MovementType != ItemMovementType.OpeningBalance)
            .OrderBy(movement => movement.MovementType)
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Contains(
            movements,
            movement =>
                movement.MovementType == ItemMovementType.AdjustmentIncrease &&
                movement.ItemId == 2 &&
                movement.QuantityIn == 3m &&
                movement.UnitCost == 4m &&
                movement.AverageCostAfter == 4m &&
                movement.InventoryValueAfter == 12m);
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
                increase.DocumentDate,
                increase.Direction,
                increase.Reason,
                increase.Lines.Select(line =>
                    AdjustmentLine(
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
    public async Task InventoryCount_ReconcileRequiresCostForEveryIncrease()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateInventoryCountService();
        var created = (await service.AddAsync(
            CountRequest())).Value;
        var updated = await UpdateCountAsync(
            service,
            created,
            physicalByItem: new Dictionary<int, decimal?>
            {
                [1] = 10m,
                [2] = 3m
            });
        database.Context.ChangeTracker.Clear();

        var result = await service.ReconcileAsync(
            created.Id,
            new InventoryCountReconcileRequest(updated.RowVersion));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "InventoryCounts.IncreaseCostsRequired",
            result.Error.Code);
        Assert.Empty(await database.Context.StockAdjustments.ToListAsync());
    }

    [Fact]
    public async Task InventoryCount_NoDifferencesCreatesNoAdjustments()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateInventoryCountService();
        var created = (await service.AddAsync(
            CountRequest())).Value;
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
        Assert.Empty(await database.Context.ItemMovements
            .Where(movement =>
                movement.MovementType != ItemMovementType.OpeningBalance)
            .ToListAsync());
    }

    [Fact]
    public async Task InventoryCount_ReconcileRejectsMovementAfterSnapshot()
    {
        await using var database =
            await InventoryDocumentTestDatabase.CreateAsync();
        var service = database.CreateInventoryCountService();
        var created = (await service.AddAsync(
            CountRequest())).Value;

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
            CountRequest())).Value;

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
        StockAdjustmentDirection direction,
        decimal quantity) =>
        new(
            1,
            new DateOnly(2026, 7, 28),
            direction,
            null,
            [
                direction == StockAdjustmentDirection.Increase
                    ? AdjustmentLine(1, quantity, null)
                    : new StockAdjustmentLineRequest(1, quantity, null)
            ]);

    private static StockAdjustmentLineRequest AdjustmentLine(
        int itemId,
        decimal quantity,
        string? reason) =>
        new(itemId, quantity, reason)
        {
            UnitCost = 0m
        };

    private static InventoryCountRequest CountRequest() =>
        new(
            1,
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
