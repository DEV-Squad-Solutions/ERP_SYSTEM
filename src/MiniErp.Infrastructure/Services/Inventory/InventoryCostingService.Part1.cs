using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.Inventory.InventoryErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Inventory;

public sealed partial class InventoryCostingService
{
    public async Task LockAsync(
        IReadOnlyCollection<InventoryCostingKey> keys,
        CancellationToken cancellationToken = default)
    {
        var lockKeys = await LoadTransferKeysAsync(keys, cancellationToken);
        var orderedKeys = lockKeys
            .Distinct()
            .OrderBy(key => key.StoreId)
            .ThenBy(key => key.ItemId);

        foreach (var key in orderedKeys)
        {
            await LockBalanceAsync(
                key,
                createIfMissing: false,
                cancellationToken);
        }
    }

    public async Task<Error?> RecalculateAsync(
        IReadOnlyCollection<InventoryCostingKey> keys,
        CancellationToken cancellationToken = default)
    {
        var keyComparer = Comparer<InventoryCostingKey>.Create((left, right) =>
        {
            var fiscalYearComparison = Nullable.Compare(
                left.FiscalYearId,
                right.FiscalYearId);
            if (fiscalYearComparison != 0)
            {
                return fiscalYearComparison;
            }

            var storeComparison = left.StoreId.CompareTo(right.StoreId);
            return storeComparison != 0
                ? storeComparison
                : left.ItemId.CompareTo(right.ItemId);
        });
        var normalizedKeys = EnsureFiscalYearKeys(keys);
        var pendingKeys = new SortedSet<InventoryCostingKey>(
            normalizedKeys,
            keyComparer);
        var recalculatedKeys = new HashSet<InventoryCostingKey>();
        var processedTransferFingerprints =
            new Dictionary<InventoryCostingKey, string>();
        var transferKeys = await LoadTransferKeysAsync(
            pendingKeys,
            cancellationToken);
        foreach (var transferKey in transferKeys)
        {
            pendingKeys.Add(transferKey);
        }

        await LockAsync(pendingKeys.ToArray(), cancellationToken);

        var maxIterations = Math.Max(
            1000L,
            (long)pendingKeys.Count * 4L);
        var iterations = 0L;

        while (pendingKeys.Count > 0)
        {
            if (++iterations > maxIterations)
            {
                return TransferCostingCycle();
            }

            var key = pendingKeys.Min!;
            pendingKeys.Remove(key);

            var transferFingerprint = await BuildTransferInputFingerprintAsync(
                key,
                cancellationToken);
            if (processedTransferFingerprints.TryGetValue(
                    key,
                    out var previousFingerprint) &&
                string.Equals(
                    previousFingerprint,
                    transferFingerprint,
                    StringComparison.Ordinal))
            {
                continue;
            }

            processedTransferFingerprints[key] = transferFingerprint;
            recalculatedKeys.Add(key);
            var balance = await LockBalanceAsync(
                key,
                createIfMissing: true,
                cancellationToken);

            var error = await ReplayKeyAsync(
                key,
                balance!,
                cancellationToken);
            if (error is not null)
            {
                return error;
            }

            var transferSynchronization =
                await SynchronizeTransferInboundCostsAsync(
                key,
                cancellationToken);
            if (transferSynchronization.Error is not null)
            {
                return transferSynchronization.Error;
            }

            foreach (var dependentKey in transferSynchronization.Keys)
            {
                pendingKeys.Add(dependentKey);
            }
        }

        if (inventoryCostPostingSynchronizer is not null &&
            recalculatedKeys.Count > 0)
        {
            // Posting services read persisted movement costs. The save remains
            // inside the caller's transaction, so costing and journals commit
            // or roll back together.
            await dbContext.SaveChangesAsync(cancellationToken);
            return await inventoryCostPostingSynchronizer.SynchronizeAsync(
                recalculatedKeys,
                cancellationToken);
        }

        return null;
    }

    private static IReadOnlyCollection<InventoryCostingKey>
        EnsureFiscalYearKeys(
            IReadOnlyCollection<InventoryCostingKey> keys)
    {
        // Defaulting a missing fiscal year to the current one silently
        // recalculates the wrong year for documents dated in another open
        // year, so every caller must pass the document's own fiscal year.
        if (keys.Any(key => !key.FiscalYearId.HasValue))
        {
            throw new InvalidOperationException(
                "Inventory costing recalculation requires the fiscal year " +
                "of every store/item key.");
        }

        return keys.Distinct().ToArray();
    }

    private Task<int?> ResolveFiscalYearIdAsync(
        DateOnly date,
        CancellationToken cancellationToken) =>
        dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                date >= year.StartDate &&
                date <= year.EndDate)
            .Select(year => (int?)year.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<TransferSynchronizationResult>
        SynchronizeTransferInboundCostsAsync(
            InventoryCostingKey sourceKey,
            CancellationToken cancellationToken)
    {
        var outboundMovements = await dbContext.ItemMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.FiscalYearId == sourceKey.FiscalYearId &&
                movement.StoreId == sourceKey.StoreId &&
                movement.ItemId == sourceKey.ItemId &&
                movement.MovementType == ItemMovementType.TransferOut)
            .ToListAsync(cancellationToken);
        if (outboundMovements.Count == 0)
        {
            return TransferSynchronizationResult.Empty;
        }

        var referenceIds = outboundMovements
            .Select(movement => movement.ReferenceId)
            .Distinct()
            .ToArray();
        var inboundMovements = await dbContext.ItemMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.FiscalYearId == sourceKey.FiscalYearId &&
                movement.ItemId == sourceKey.ItemId &&
                movement.MovementType == ItemMovementType.TransferIn &&
                referenceIds.Contains(movement.ReferenceId))
            .ToListAsync(cancellationToken);
        var inboundByReference = inboundMovements.ToDictionary(
            movement => movement.ReferenceId);
        var dependentKeys = new HashSet<InventoryCostingKey>();

        foreach (var outbound in outboundMovements)
        {
            if (!inboundByReference.TryGetValue(
                    outbound.ReferenceId,
                    out var inbound))
            {
                continue;
            }

            if (!outbound.UnitCost.HasValue)
            {
                if (inbound.UnitCost != 0m)
                {
                    inbound.SetTransferUnitCost(0m);
                    dependentKeys.Add(new InventoryCostingKey(
                        inbound.StoreId,
                        inbound.ItemId,
                        inbound.FiscalYearId));
                }

                continue;
            }

            if (
                inbound.UnitCost == outbound.UnitCost)
            {
                continue;
            }

            inbound.SetTransferUnitCost(outbound.UnitCost.Value);
            dependentKeys.Add(new InventoryCostingKey(
                inbound.StoreId,
                inbound.ItemId,
                inbound.FiscalYearId));
        }

        return new TransferSynchronizationResult(null, dependentKeys);
    }

    private async Task<IReadOnlyCollection<InventoryCostingKey>>
        LoadTransferKeysAsync(
            IReadOnlyCollection<InventoryCostingKey> initialKeys,
            CancellationToken cancellationToken)
    {
        var fiscalYearIds = initialKeys
            .Where(key => key.FiscalYearId.HasValue)
            .Select(key => key.FiscalYearId!.Value)
            .Distinct()
            .ToArray();
        var transferRows = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                fiscalYearIds.Contains(movement.FiscalYearId) &&
                (movement.MovementType == ItemMovementType.TransferIn ||
                 movement.MovementType == ItemMovementType.TransferOut))
            .Select(movement => new
            {
                movement.ReferenceId,
                movement.FiscalYearId,
                movement.StoreId,
                movement.ItemId,
                movement.MovementType
            })
            .ToListAsync(cancellationToken);
        var adjacency = new Dictionary<InventoryCostingKey, HashSet<InventoryCostingKey>>();
        foreach (var group in transferRows.GroupBy(row =>
                     (row.FiscalYearId, row.ReferenceId, row.ItemId)))
        {
            var groupKeys = group
                .Select(row => new InventoryCostingKey(
                    row.StoreId,
                    row.ItemId,
                    row.FiscalYearId))
                .Distinct()
                .ToArray();
            foreach (var groupKey in groupKeys)
            {
                if (!adjacency.TryGetValue(groupKey, out var neighbours))
                {
                    neighbours = [];
                    adjacency[groupKey] = neighbours;
                }

                foreach (var neighbour in groupKeys.Where(candidate => candidate != groupKey))
                {
                    neighbours.Add(neighbour);
                }
            }
        }

        var closure = new HashSet<InventoryCostingKey>(initialKeys);
        var queue = new Queue<InventoryCostingKey>(initialKeys);
        while (queue.Count > 0)
        {
            var key = queue.Dequeue();
            if (!adjacency.TryGetValue(key, out var neighbours))
            {
                continue;
            }

            foreach (var neighbour in neighbours)
            {
                if (closure.Add(neighbour))
                {
                    queue.Enqueue(neighbour);
                }
            }
        }

        return closure;
    }

    private async Task<string> BuildTransferInputFingerprintAsync(
        InventoryCostingKey key,
        CancellationToken cancellationToken)
    {
        var movements = await dbContext.ItemMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.FiscalYearId == key.FiscalYearId &&
                movement.StoreId == key.StoreId &&
                movement.ItemId == key.ItemId &&
                movement.MovementType == ItemMovementType.TransferIn)
            .OrderBy(movement => movement.Id)
            .ToListAsync(cancellationToken);

        return string.Join(
            ";",
            movements
                .OrderBy(movement => movement.Id)
                .Select(movement =>
                    $"{movement.Id}:{movement.UnitCost?.ToString("G29", System.Globalization.CultureInfo.InvariantCulture) ?? "null"}"));
    }

    private sealed record TransferSynchronizationResult(
        Error? Error,
        IReadOnlyCollection<InventoryCostingKey> Keys)
    {
        public static TransferSynchronizationResult Empty { get; } =
            new(null, []);
    }

    public async Task<IReadOnlyDictionary<int, InventoryCostSnapshot>>
        GetSnapshotsAsync(
            int storeId,
            IReadOnlyCollection<int> itemIds,
            DateOnly asOfDate,
            CancellationToken cancellationToken = default)
    {
        var distinctItemIds = itemIds.Distinct().ToArray();
        if (distinctItemIds.Length == 0)
        {
            return new Dictionary<int, InventoryCostSnapshot>();
        }

        var fiscalYearId = await ResolveFiscalYearIdAsync(
            asOfDate,
            cancellationToken);
        if (!fiscalYearId.HasValue)
        {
            return distinctItemIds.ToDictionary(
                itemId => itemId,
                itemId => new InventoryCostSnapshot(0m, 0m, 0m));
        }

        var snapshots = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.FiscalYearId == fiscalYearId.Value &&
                movement.StoreId == storeId &&
                distinctItemIds.Contains(movement.ItemId) &&
                movement.MovementDate <= asOfDate)
            .GroupBy(movement => movement.ItemId)
            .Select(group => group
                .OrderByDescending(movement => movement.MovementDate)
                .ThenByDescending(movement => movement.CreatedOn)
                .ThenByDescending(movement => movement.Id)
                .Select(movement => new
                {
                    movement.ItemId,
                    movement.QuantityAfter,
                    movement.AverageCostAfter,
                    movement.InventoryValueAfter
                })
                .First())
            .ToDictionaryAsync(
                snapshot => snapshot.ItemId,
                snapshot => new InventoryCostSnapshot(
                    snapshot.QuantityAfter,
                    snapshot.AverageCostAfter,
                    snapshot.InventoryValueAfter),
                cancellationToken);

        return distinctItemIds.ToDictionary(
            itemId => itemId,
            itemId => snapshots.GetValueOrDefault(
                itemId,
                new InventoryCostSnapshot(0m, 0m, 0m)));
    }

    private async Task<ItemStoreBalance?> LockBalanceAsync(
        InventoryCostingKey key,
        bool createIfMissing,
        CancellationToken cancellationToken)
    {
        ItemStoreBalance? balance;

        if (dbContext.Database.IsSqlServer() &&
            dbContext.Database.CurrentTransaction is not null)
        {
            balance = await dbContext.ItemStoreBalances
                .FromSqlInterpolated(
                    $"""
                     SELECT *
                     FROM [ItemStoreBalances] WITH (UPDLOCK, HOLDLOCK)
                     WHERE [CompanyId] = {companyId}
                       AND [StoreId] = {key.StoreId}
                       AND [ItemId] = {key.ItemId}
                     """)
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(cancellationToken);
        }
        else
        {
            balance = await dbContext.ItemStoreBalances
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    itemStoreBalance =>
                        itemStoreBalance.CompanyId == companyId &&
                        itemStoreBalance.StoreId == key.StoreId &&
                        itemStoreBalance.ItemId == key.ItemId,
                    cancellationToken);
        }

        if (balance is not null)
        {
            if (createIfMissing && balance.IsDeleted)
            {
                balance.IsDeleted = false;
            }

            return balance;
        }

        if (!createIfMissing)
        {
            return null;
        }

        balance = new ItemStoreBalance
        {
            CompanyId = companyId,
            StoreId = key.StoreId,
            ItemId = key.ItemId
        };
        dbContext.ItemStoreBalances.Add(balance);
        return balance;
    }

    private async Task<Error?> ReplayKeyAsync(
        InventoryCostingKey key,
        ItemStoreBalance balance,
        CancellationToken cancellationToken)
    {
        var movements = await dbContext.ItemMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.FiscalYearId == key.FiscalYearId &&
                movement.StoreId == key.StoreId &&
                movement.ItemId == key.ItemId)
            .OrderBy(movement => movement.MovementDate)
            .ThenBy(movement => movement.CreatedOn)
            .ThenBy(movement => movement.Id)
            .ToListAsync(cancellationToken);

        var originalSnapshots = movements.ToDictionary(
            movement => movement.Id,
            movement => new CostSnapshot(
                GetOriginalMovementDate(movement),
                movement.CostStatus,
                movement.PendingCostQuantity,
                movement.UnitCost,
                movement.TotalCost,
                movement.QuantityAfter,
                movement.AverageCostAfter,
                movement.InventoryValueAfter));

        var allocations = await dbContext.InventoryCostAllocations
            .Where(allocation =>
                allocation.CompanyId == companyId &&
                allocation.StoreId == key.StoreId &&
                allocation.ItemId == key.ItemId &&
                allocation.OutboundMovement.FiscalYearId ==
                    key.FiscalYearId &&
                allocation.InboundMovement.FiscalYearId ==
                    key.FiscalYearId)
            .ToListAsync(cancellationToken);
        dbContext.InventoryCostAllocations.RemoveRange(allocations);

        var initialAllocations = new List<InventoryCostAllocation>();
        var initialReplay = await ReplayTimelineAsync(
            movements,
            new Dictionary<int, decimal>(),
            initialAllocations,
            cancellationToken);
        if (initialReplay.Error is not null)
        {
            return initialReplay.Error;
        }

        var sourceCostOverrides = BuildSalesReturnSourceCostOverrides(
            initialReplay.PendingSalesReturns);
        if (sourceCostOverrides.Count == 0)
        {
            var openError = await EnsureCostChangesOpenAsync(
                movements,
                originalSnapshots,
                cancellationToken);
            if (openError is not null)
            {
                return openError;
            }

            dbContext.InventoryCostAllocations.AddRange(initialAllocations);
            balance.Apply(
                initialReplay.Quantity,
                initialReplay.AverageCost,
                initialReplay.InventoryValue);
            return null;
        }

        var finalAllocations = new List<InventoryCostAllocation>();
        var finalReplay = await ReplayTimelineAsync(
            movements,
            sourceCostOverrides,
            finalAllocations,
            cancellationToken);
        if (finalReplay.Error is not null)
        {
            return finalReplay.Error;
        }

        var finalOpenError = await EnsureCostChangesOpenAsync(
            movements,
            originalSnapshots,
            cancellationToken);
        if (finalOpenError is not null)
        {
            return finalOpenError;
        }

        dbContext.InventoryCostAllocations.AddRange(finalAllocations);
        balance.Apply(
            finalReplay.Quantity,
            finalReplay.AverageCost,
            finalReplay.InventoryValue);
        return null;
    }
}
