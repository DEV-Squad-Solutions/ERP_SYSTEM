using System.Data;
using static MiniErp.Application.Features.InventoryCounts.InventoryCountErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.InventoryCounts;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.InventoryCounts;

public sealed class InventoryCountService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IInventoryStockService inventoryStockService,
    IInventoryCostingService inventoryCostingService,
    TimeProvider timeProvider,
    IInventoryPostingService? inventoryPostingService = null)
    : IInventoryCountService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<InventoryCountListResponse>>>
        GetAllAsync(
            PaginationRequest pagination,
            InventoryCountFilterRequest? filters = null,
            CancellationToken cancellationToken = default)
    {
        filters ??= new InventoryCountFilterRequest();
        var filterError = ValidateFilters(filters);
        if (filterError is not null)
        {
            return Result<PagedResponse<InventoryCountListResponse>>.Failure(
                filterError);
        }

        var documentNumber = filters.DocumentNumber?.Trim();
        var query = dbContext.InventoryCounts
            .AsNoTracking()
            .Where(count => count.CompanyId == companyId)
            .Where(count =>
                string.IsNullOrEmpty(documentNumber) ||
                count.DocumentNumber.Contains(documentNumber))
            .Where(count =>
                !filters.StoreId.HasValue ||
                count.StoreId == filters.StoreId.Value)
            .Where(count =>
                !filters.IsReconciled.HasValue ||
                filters.IsReconciled.Value ==
                count.ReconciledAt.HasValue)
            .Where(count =>
                !filters.FromDate.HasValue ||
                count.CountDate >= filters.FromDate.Value)
            .Where(count =>
                !filters.ToDate.HasValue ||
                count.CountDate <= filters.ToDate.Value)
            .OrderByDescending(count => count.CountDate)
            .ThenByDescending(count => count.Id);

        return await paginationService.PaginateAsync<
            InventoryCount,
            InventoryCountListResponse>(
                query,
                pagination,
                cancellationToken);
    }

    public async Task<Result<InventoryCountResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<InventoryCountResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<InventoryCountResponse>.Failure(NotFound(id))
            : Result<InventoryCountResponse>.Success(response);
    }

    public async Task<Result<InventoryCountResponse>> AddAsync(
        InventoryCountRequest request,
        CancellationToken cancellationToken = default)
    {
        var requested = request.Adapt<InventoryCount>();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var storeError = await ValidateStoreAsync(
            requested.StoreId,
            cancellationToken);
        if (storeError is not null)
        {
            return Result<InventoryCountResponse>.Failure(storeError);
        }

        requested.DocumentNumber = await EntityIdentifierGenerator
            .GenerateUniqueAsync(
                dbContext,
                prefix: "IC",
                companyId: companyId,
                existingIdentifiers: dbContext.InventoryCounts
                    .IgnoreQueryFilters()
                    .Where(entity => entity.CompanyId == companyId)
                    .Select(entity => entity.DocumentNumber),
                cancellationToken);

        var items = await dbContext.Items
            .AsNoTracking()
            .Where(item =>
                item.CompanyId == companyId &&
                item.IsActive &&
                item.ItemUnit.IsActive)
            .OrderBy(item => item.Id)
            .Select(item => new ItemSnapshot(
                item.Id,
                item.ItemUnitId))
            .ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            return Result<InventoryCountResponse>.Failure(
                NoEligibleItems());
        }

        var itemIds = items.Select(item => item.Id).ToArray();
        var balances = await inventoryStockService.GetBalancesAsync(
            requested.StoreId,
            itemIds,
            requested.CountDate,
            cancellationToken: cancellationToken);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        requested.CompanyId = companyId;
        requested.SnapshotTakenAt = utcNow;
        requested.ReconciledAt = null;
        requested.Touch(utcNow);

        foreach (var item in items)
        {
            requested.Lines.Add(
                new InventoryCountLine
                {
                    CompanyId = companyId,
                    ItemId = item.Id,
                    ItemUnitId = item.ItemUnitId,
                    SystemQuantity = balances[item.Id],
                    PhysicalQuantity = null
                });
        }

        dbContext.InventoryCounts.Add(requested);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(requested.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<InventoryCountResponse>.Success(response);
    }

    public async Task<Result<InventoryCountResponse>> UpdateAsync(
        int id,
        InventoryCountUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<InventoryCountResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<InventoryCountResponse>.Failure(
                RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var count = await LoadForWriteAsync(id, cancellationToken);
        if (count is null)
        {
            return Result<InventoryCountResponse>.Failure(NotFound(id));
        }

        if (count.ReconciledAt.HasValue)
        {
            return Result<InventoryCountResponse>.Failure(
                ReconciledImmutable());
        }

        if (!count.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<InventoryCountResponse>.Failure(Concurrency());
        }

        var lineError = ValidateReplacementLines(count, request.Lines);
        if (lineError is not null)
        {
            return Result<InventoryCountResponse>.Failure(lineError);
        }

        var entry = dbContext.Entry(count);
        entry.Property(item => item.RowVersion).OriginalValue =
            request.RowVersion;

        count.Notes = Normalize(request.Notes);
        var incomingByItem = request.Lines.ToDictionary(line => line.ItemId);
        foreach (var line in count.Lines)
        {
            var incoming = incomingByItem[line.ItemId];
            line.PhysicalQuantity = incoming.PhysicalQuantity;
            line.Notes = Normalize(incoming.Notes);
        }

        count.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(item => item.LastModifiedAt).IsModified = true;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InventoryCountResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<InventoryCountResponse>.Success(response);
    }

    public async Task<Result<InventoryCountResponse>> ReconcileAsync(
        int id,
        InventoryCountReconcileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<InventoryCountResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<InventoryCountResponse>.Failure(
                ReconcileRowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var count = await LoadForWriteAsync(id, cancellationToken);
        if (count is null)
        {
            return Result<InventoryCountResponse>.Failure(NotFound(id));
        }

        if (count.ReconciledAt.HasValue)
        {
            return Result<InventoryCountResponse>.Failure(AlreadyReconciled());
        }

        if (!count.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<InventoryCountResponse>.Failure(Concurrency());
        }

        var missingPhysicalItemIds = count.Lines
            .Where(line => !line.PhysicalQuantity.HasValue)
            .Select(line => line.ItemId)
            .ToArray();
        if (missingPhysicalItemIds.Length > 0)
        {
            return Result<InventoryCountResponse>.Failure(
                PhysicalQuantitiesRequired(missingPhysicalItemIds));
        }

        var itemIds = count.Lines.Select(line => line.ItemId).ToArray();

        var increaseItemIds = count.Lines
            .Where(line =>
                line.PhysicalQuantity!.Value > line.SystemQuantity)
            .Select(line => line.ItemId)
            .ToHashSet();
        var requestedIncreaseCosts = request.IncreaseCosts ?? [];
        if (requestedIncreaseCosts.Any(cost =>
                cost.ItemId <= 0 ||
                cost.UnitCost < 0m) ||
            requestedIncreaseCosts
                .Select(cost => cost.ItemId)
                .Distinct()
                .Count() != requestedIncreaseCosts.Count)
        {
            return Result<InventoryCountResponse>.Failure(
                IncreaseCostsInvalid());
        }

        var increaseCosts = requestedIncreaseCosts.ToDictionary(
            cost => cost.ItemId,
            cost => cost.UnitCost);
        if (!increaseCosts.Keys.ToHashSet().SetEquals(increaseItemIds))
        {
            return Result<InventoryCountResponse>.Failure(
                IncreaseCostsRequired(increaseItemIds));
        }

        await inventoryCostingService.LockAsync(
            count.Lines
                .Where(line =>
                    line.PhysicalQuantity!.Value != line.SystemQuantity)
                .Select(line => new InventoryCostingKey(
                    count.StoreId,
                    line.ItemId))
                .Distinct()
                .ToArray(),
            cancellationToken);

        var stockChanged = await inventoryStockService
            .HasStockChangesSinceAsync(
                count.StoreId,
                itemIds,
                count.SnapshotTakenAt,
                cancellationToken);
        var currentBalances = await inventoryStockService.GetBalancesAsync(
            count.StoreId,
            itemIds,
            count.CountDate,
            cancellationToken: cancellationToken);
        var balanceChanged = count.Lines.Any(line =>
            currentBalances[line.ItemId] != line.SystemQuantity);
        if (stockChanged || balanceChanged)
        {
            return Result<InventoryCountResponse>.Failure(SnapshotStale());
        }

        var decreaseLines = count.Lines
            .Where(line =>
                line.PhysicalQuantity!.Value < line.SystemQuantity)
            .Select(line => new InventoryStockLine(
                line.ItemId,
                line.SystemQuantity - line.PhysicalQuantity!.Value))
            .ToArray();
        if (decreaseLines.Length > 0)
        {
            var stockError = await inventoryStockService.ValidateTimelineAsync(
                new InventoryStockProposal(
                    count.StoreId,
                    count.CountDate,
                    IsInbound: false,
                    decreaseLines,
                    ReplacedMovement: null,
                    $"تسوية مستند الجرد {count.DocumentNumber}",
                    nameof(InventoryCountUpdateRequest.Lines)),
                cancellationToken);
            if (stockError is not null)
            {
                return Result<InventoryCountResponse>.Failure(stockError);
            }
        }

        var adjustmentNumbers = dbContext.StockAdjustments
            .IgnoreQueryFilters()
            .Where(entity => entity.CompanyId == companyId)
            .Select(entity => entity.DocumentNumber);
        var increaseNumber = increaseItemIds.Count > 0
            ? await EntityIdentifierGenerator.GenerateUniqueAsync(
                dbContext,
                prefix: "ADJ",
                companyId: companyId,
                existingIdentifiers: adjustmentNumbers,
                cancellationToken)
            : string.Empty;
        var decreaseNumber = decreaseLines.Length > 0
            ? await EntityIdentifierGenerator.GenerateUniqueAsync(
                dbContext,
                prefix: "ADJ",
                companyId: companyId,
                existingIdentifiers: adjustmentNumbers,
                cancellationToken)
            : string.Empty;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var increase = CreateGeneratedAdjustment(
            count,
            StockAdjustmentDirection.Increase,
            increaseNumber,
            utcNow,
            increaseCosts);
        var decrease = CreateGeneratedAdjustment(
            count,
            StockAdjustmentDirection.Decrease,
            decreaseNumber,
            utcNow);

        var generatedNumbers = new[] { increase, decrease }
            .Where(adjustment => adjustment is not null)
            .Select(adjustment => adjustment!.DocumentNumber)
            .ToArray();
        if (generatedNumbers.Length > 0 &&
            await GeneratedDocumentNumberExistsAsync(
                generatedNumbers,
                cancellationToken))
        {
            return Result<InventoryCountResponse>.Failure(
                GeneratedDocumentNumberConflict());
        }

        if (increase is not null)
        {
            dbContext.StockAdjustments.Add(increase);
        }

        if (decrease is not null)
        {
            dbContext.StockAdjustments.Add(decrease);
        }

        var entry = dbContext.Entry(count);
        entry.Property(item => item.RowVersion).OriginalValue =
            request.RowVersion;
        count.ReconciledAt = utcNow;
        count.Touch(utcNow);
        entry.Property(item => item.LastModifiedAt).IsModified = true;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            if (increase is not null)
            {
                AddAdjustmentMovements(increase);
            }

            if (decrease is not null)
            {
                AddAdjustmentMovements(decrease);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var costingKeys = new[] { increase, decrease }
                .Where(adjustment => adjustment is not null)
                .SelectMany(adjustment => adjustment!.Lines.Select(line =>
                    new InventoryCostingKey(
                        adjustment.StoreId,
                        line.ItemId)))
                .Distinct()
                .ToArray();
            var costingError = await inventoryCostingService.RecalculateAsync(
                costingKeys,
                cancellationToken);
            if (costingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<InventoryCountResponse>.Failure(costingError);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (inventoryPostingService is not null)
            {
                foreach (var generatedAdjustment in new[]
                         {
                             increase,
                             decrease
                         }.Where(adjustment => adjustment is not null))
                {
                    var postingResult = await inventoryPostingService
                        .SynchronizeStockAdjustmentAsync(
                            generatedAdjustment!.Id,
                            cancellationToken);
                    if (postingResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        dbContext.ChangeTracker.Clear();
                        return Result<InventoryCountResponse>.Failure(
                            postingResult.Errors);
                    }
                }
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InventoryCountResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<InventoryCountResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        byte[]? rowVersion,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        if (rowVersion is not { Length: 8 })
        {
            return Result.Failure(RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var count = await LoadForWriteAsync(id, cancellationToken);
        if (count is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (!count.RowVersion.SequenceEqual(rowVersion))
        {
            return Result.Failure(Concurrency());
        }

        if (count.ReconciledAt.HasValue ||
            await dbContext.StockAdjustments.AnyAsync(
                adjustment =>
                    adjustment.CompanyId == companyId &&
                    adjustment.SourceInventoryCountId == id,
                cancellationToken))
        {
            return Result.Failure(ReconciledImmutable());
        }

        var entry = dbContext.Entry(count);
        entry.Property(item => item.RowVersion).OriginalValue = rowVersion;

        dbContext.InventoryCountLines.RemoveRange(count.Lines);
        dbContext.InventoryCounts.Remove(count);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<InventoryCountResponse> ProjectResponseQuery(int id) =>
        dbContext.InventoryCounts
            .Where(count =>
                count.CompanyId == companyId &&
                count.Id == id)
            .ProjectToType<InventoryCountResponse>();

    private Task<InventoryCount?> LoadForWriteAsync(
        int id,
        CancellationToken cancellationToken) =>
        dbContext.InventoryCounts
            .Include(count => count.Lines)
            .FirstOrDefaultAsync(
                count =>
                    count.CompanyId == companyId &&
                    count.Id == id,
                cancellationToken);

    private async Task<Error?> ValidateStoreAsync(
        int storeId,
        CancellationToken cancellationToken)
    {
        var store = await dbContext.Stores
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == storeId)
            .Select(candidate => new
            {
                candidate.IsActive,
                candidate.IsContainerStore
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (store is null)
        {
            return StoreNotFound(storeId);
        }

        if (!store.IsActive)
        {
            return StoreInactive();
        }

        return store.IsContainerStore
            ? ContainerStoreNotAllowed()
            : null;
    }

    private Task<bool> DocumentNumberExistsAsync(
        string documentNumber,
        CancellationToken cancellationToken) =>
        dbContext.InventoryCounts.AnyAsync(
            count =>
                count.CompanyId == companyId &&
                count.DocumentNumber == documentNumber,
            cancellationToken);

    private Task<bool> GeneratedDocumentNumberExistsAsync(
        IReadOnlyCollection<string> documentNumbers,
        CancellationToken cancellationToken) =>
        dbContext.StockAdjustments.AnyAsync(
            adjustment =>
                adjustment.CompanyId == companyId &&
                documentNumbers.Contains(adjustment.DocumentNumber),
            cancellationToken);

    private static Error? ValidateReplacementLines(
        InventoryCount count,
        IReadOnlyCollection<InventoryCountLineUpdateRequest> lines)
    {
        if (lines.Any(line =>
                line.PhysicalQuantity.HasValue &&
                line.PhysicalQuantity.Value < 0m))
        {
            return NegativePhysicalQuantity();
        }

        if (lines.Count != count.Lines.Count ||
            lines.Any(line => line.ItemId <= 0) ||
            lines.Select(line => line.ItemId).Distinct().Count() != lines.Count)
        {
            return LinesDoNotMatchSnapshot();
        }

        var expectedItemIds = count.Lines
            .Select(line => line.ItemId)
            .ToHashSet();
        return lines.All(line => expectedItemIds.Contains(line.ItemId))
            ? null
            : LinesDoNotMatchSnapshot();
    }

    private StockAdjustment? CreateGeneratedAdjustment(
        InventoryCount count,
        StockAdjustmentDirection direction,
        string documentNumber,
        DateTime utcNow,
        IReadOnlyDictionary<int, decimal>? increaseCosts = null)
    {
        var adjustment = new StockAdjustment
        {
            CompanyId = companyId,
            StoreId = count.StoreId,
            DocumentNumber = documentNumber,
            DocumentDate = count.CountDate,
            Direction = direction,
            Reason = $"Generated from inventory count {count.DocumentNumber}",
            SourceInventoryCountId = count.Id
        };

        foreach (var countLine in count.Lines)
        {
            var difference =
                countLine.PhysicalQuantity!.Value -
                countLine.SystemQuantity;
            var quantity = direction == StockAdjustmentDirection.Increase
                ? difference
                : -difference;
            if (quantity <= 0m)
            {
                continue;
            }

            adjustment.Lines.Add(
                new StockAdjustmentLine
                {
                    CompanyId = companyId,
                    ItemId = countLine.ItemId,
                    ItemUnitId = countLine.ItemUnitId,
                    Quantity = quantity,
                    UnitCost =
                        direction == StockAdjustmentDirection.Increase
                            ? increaseCosts![countLine.ItemId]
                            : null,
                    Reason = countLine.Notes
                });
        }

        if (adjustment.Lines.Count == 0)
        {
            return null;
        }

        adjustment.Touch(utcNow);
        return adjustment;
    }

    private void AddAdjustmentMovements(StockAdjustment adjustment)
    {
        var inbound = StockAdjustmentMovementRules.IsInbound(
            adjustment.Direction);
        var movementType = StockAdjustmentMovementRules.GetMovementType(
            adjustment.Direction);

        foreach (var line in adjustment.Lines)
        {
            dbContext.ItemMovements.Add(
                new ItemMovement
                {
                    CompanyId = companyId,
                    StoreId = adjustment.StoreId,
                    ItemId = line.ItemId,
                    ItemUnitId = line.ItemUnitId,
                    MovementType = movementType,
                    ReferenceId = adjustment.Id,
                    ReferenceNumber = adjustment.DocumentNumber,
                    MovementDate = adjustment.DocumentDate,
                    QuantityIn = inbound ? line.Quantity : 0m,
                    QuantityOut = inbound ? 0m : line.Quantity,
                    Description =
                        $"Inventory count {adjustment.SourceInventoryCountId}"
                });
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Error? ValidateFilters(InventoryCountFilterRequest filters)
    {
        if (filters.StoreId is <= 0 ||
            filters.ToDate < filters.FromDate)
        {
            return FiltersInvalid();
        }

        return null;
    }

    private sealed record ItemSnapshot(
        int Id,
        int ItemUnitId);

}
