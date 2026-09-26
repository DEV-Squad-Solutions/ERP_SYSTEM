using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.Inventory.InventoryErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Inventory;

public sealed class InventoryCostingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider,
    IInventoryCostPostingSynchronizer?
        inventoryCostPostingSynchronizer = null,
    IFiscalYearPeriodGuard? fiscalYearPeriodGuard = null)
    : IInventoryCostingService
{
    private const string CostFieldName = "unitCost";
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task LockAsync(
        IReadOnlyCollection<InventoryCostingKey> keys,
        CancellationToken cancellationToken = default)
    {
        var orderedKeys = keys
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
            var storeComparison = left.StoreId.CompareTo(right.StoreId);
            return storeComparison != 0
                ? storeComparison
                : left.ItemId.CompareTo(right.ItemId);
        });
        var pendingKeys = new SortedSet<InventoryCostingKey>(keys, keyComparer);
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

    private async Task<TransferSynchronizationResult>
        SynchronizeTransferInboundCostsAsync(
            InventoryCostingKey sourceKey,
            CancellationToken cancellationToken)
    {
        var outboundMovements = await dbContext.ItemMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
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
                        inbound.ItemId));
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
                inbound.ItemId));
        }

        return new TransferSynchronizationResult(null, dependentKeys);
    }

    private async Task<IReadOnlyCollection<InventoryCostingKey>>
        LoadTransferKeysAsync(
            IReadOnlyCollection<InventoryCostingKey> initialKeys,
            CancellationToken cancellationToken)
    {
        var transferRows = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                (movement.MovementType == ItemMovementType.TransferIn ||
                 movement.MovementType == ItemMovementType.TransferOut))
            .Select(movement => new
            {
                movement.ReferenceId,
                movement.StoreId,
                movement.ItemId,
                movement.MovementType
            })
            .ToListAsync(cancellationToken);
        var adjacency = new Dictionary<InventoryCostingKey, HashSet<InventoryCostingKey>>();
        foreach (var group in transferRows.GroupBy(row =>
                     (row.ReferenceId, row.ItemId)))
        {
            var groupKeys = group
                .Select(row => new InventoryCostingKey(row.StoreId, row.ItemId))
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

        var snapshots = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
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
                allocation.ItemId == key.ItemId)
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

    private async Task<Error?> EnsureCostChangesOpenAsync(
        IReadOnlyCollection<ItemMovement> movements,
        IReadOnlyDictionary<int, CostSnapshot> originalSnapshots,
        CancellationToken cancellationToken)
    {
        if (fiscalYearPeriodGuard is null)
        {
            return null;
        }

        foreach (var movement in movements)
        {
            if (!originalSnapshots.TryGetValue(movement.Id, out var original) ||
                !HasCostChanged(movement, original))
            {
                continue;
            }

            var currentResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                movement.MovementDate,
                "movementDate",
                cancellationToken);
            if (currentResult.IsFailure)
            {
                return currentResult.Error;
            }

            if (original.MovementDate != movement.MovementDate)
            {
                var originalResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                    original.MovementDate,
                    "movementDate",
                    cancellationToken);
                if (originalResult.IsFailure)
                {
                    return originalResult.Error;
                }
            }
        }

        return null;
    }

    private DateOnly GetOriginalMovementDate(ItemMovement movement)
    {
        var entry = dbContext.Entry(movement);
        return entry.State == EntityState.Added
            ? movement.MovementDate
            : entry.Property(current => current.MovementDate).OriginalValue;
    }

    private static bool HasCostChanged(
        ItemMovement movement,
        CostSnapshot original) =>
        movement.CostStatus != original.CostStatus ||
        movement.PendingCostQuantity != original.PendingCostQuantity ||
        movement.UnitCost != original.UnitCost ||
        movement.TotalCost != original.TotalCost ||
        movement.QuantityAfter != original.QuantityAfter ||
        movement.AverageCostAfter != original.AverageCostAfter ||
        movement.InventoryValueAfter != original.InventoryValueAfter ||
        movement.MovementDate != original.MovementDate;

    private sealed record CostSnapshot(
        DateOnly MovementDate,
        InventoryCostStatus CostStatus,
        decimal PendingCostQuantity,
        decimal? UnitCost,
        decimal TotalCost,
        decimal QuantityAfter,
        decimal AverageCostAfter,
        decimal InventoryValueAfter);

    private async Task<TimelineReplayResult> ReplayTimelineAsync(
        IReadOnlyList<ItemMovement> movements,
        IReadOnlyDictionary<int, decimal> salesReturnSourceCostOverrides,
        ICollection<InventoryCostAllocation> allocations,
        CancellationToken cancellationToken)
    {
        var pending = new Queue<PendingOutbound>();
        var pendingSalesReturns = new List<PendingSalesReturn>();
        var quantity = 0m;
        var averageCost = 0m;
        var inventoryValue = 0m;

        foreach (var movement in movements)
        {
            if (movement.QuantityIn > 0m)
            {
                var sourceCostResult = await ResolveInboundUnitCostAsync(
                    movement,
                    movements,
                    quantity,
                    averageCost,
                    salesReturnSourceCostOverrides,
                    cancellationToken);
                if (sourceCostResult.Error is not null)
                {
                    return TimelineReplayResult.Failure(
                        sourceCostResult.Error);
                }

                if (sourceCostResult.PendingSourceMovement is not null)
                {
                    ProcessPendingInbound(
                        movement,
                        ref quantity,
                        ref averageCost,
                        ref inventoryValue);
                    pendingSalesReturns.Add(new PendingSalesReturn(
                        Movement: movement,
                        SourceMovement:
                            sourceCostResult.PendingSourceMovement));
                    continue;
                }

                ProcessInbound(
                    movement,
                    sourceCostResult.UnitCost,
                    pending,
                    allocations,
                    ref quantity,
                    ref averageCost,
                    ref inventoryValue);
                continue;
            }

            ProcessOutbound(
                movement,
                pending,
                ref quantity,
                ref averageCost,
                ref inventoryValue);
        }

        return TimelineReplayResult.Success(
            Quantity: quantity,
            AverageCost: averageCost,
            InventoryValue: inventoryValue,
            PendingSalesReturns: pendingSalesReturns);
    }

    private async Task<InboundCostResult> ResolveInboundUnitCostAsync(
        ItemMovement movement,
        IReadOnlyCollection<ItemMovement> timeline,
        decimal quantityBefore,
        decimal averageCostBefore,
        IReadOnlyDictionary<int, decimal> salesReturnSourceCostOverrides,
        CancellationToken cancellationToken)
    {
        switch (movement.MovementType)
        {
            case ItemMovementType.Purchase:
                {
                    var purchaseCost = await ResolvePurchaseUnitCostAsync(
                        movement,
                        cancellationToken);
                    return purchaseCost.HasValue
                        ? InboundCostResult.Success(purchaseCost.Value)
                        : movement.UnitCost.HasValue
                            ? InboundCostResult.Success(movement.UnitCost.Value)
                            : InboundCostResult.Failure(
                                MissingSourceError(movement));
                }

            case ItemMovementType.SalesReturn:
                {
                    var line = await FindInvoiceLineAsync(
                        movement,
                        cancellationToken);
                    if (line is null)
                    {
                        return InboundCostResult.Failure(
                            MissingSourceError(movement));
                    }

                    if (line.SourceInvoiceLineId.HasValue)
                    {
                        var sourceLine = await dbContext.InvoiceLines
                            .AsNoTracking()
                            .Where(source =>
                                source.CompanyId == companyId &&
                                source.Id == line.SourceInvoiceLineId.Value)
                            .Select(source => new
                            {
                                source.ItemId,
                                source.InvoiceId,
                                source.Invoice.StoreId,
                                source.Invoice.InvoiceType
                            })
                            .SingleOrDefaultAsync(cancellationToken);

                        if (sourceLine is null ||
                            sourceLine.ItemId != movement.ItemId ||
                            sourceLine.StoreId != movement.StoreId ||
                            sourceLine.InvoiceType != InvoiceType.Sales)
                        {
                            return InboundCostResult.Failure(InvalidSalesReturnSource());
                        }

                        var sourceMovement = timeline.SingleOrDefault(candidate =>
                            candidate.MovementType == ItemMovementType.Sales &&
                            candidate.ReferenceId == sourceLine.InvoiceId &&
                            candidate.StoreId == movement.StoreId &&
                            candidate.ItemId == movement.ItemId);

                        if (sourceMovement is null ||
                            !ComesBefore(sourceMovement, movement))
                        {
                            return InboundCostResult.Failure(
                                InvalidSalesReturnSource());
                        }

                        if (salesReturnSourceCostOverrides.TryGetValue(
                                sourceMovement.Id,
                                out var sourceCostOverride))
                        {
                            return InboundCostResult.Success(
                                sourceCostOverride);
                        }

                        if (sourceMovement.CostStatus is
                                InventoryCostStatus.Pending or
                                InventoryCostStatus.PartiallyCosted ||
                            !sourceMovement.UnitCost.HasValue)
                        {
                            return InboundCostResult.Pending(
                                sourceMovement);
                        }

                        return InboundCostResult.Success(
                            sourceMovement.UnitCost.Value);
                    }

                    if (quantityBefore > 0m)
                    {
                        return InboundCostResult.Success(averageCostBefore);
                    }

                    return line.ReturnUnitCost.HasValue
                        ? InboundCostResult.Success(line.ReturnUnitCost.Value)
                        : InboundCostResult.Failure(ReturnUnitCostRequired());
                }

            case ItemMovementType.AdjustmentIncrease:
                {
                    var unitCost = await dbContext.StockAdjustmentLines
                        .AsNoTracking()
                        .Where(line =>
                            line.CompanyId == companyId &&
                            line.StockAdjustmentId == movement.ReferenceId &&
                            line.ItemId == movement.ItemId)
                        .Select(line => line.UnitCost)
                        .SingleOrDefaultAsync(cancellationToken);

                    return unitCost.HasValue
                        ? InboundCostResult.Success(unitCost.Value)
                        : movement.UnitCost.HasValue
                            ? InboundCostResult.Success(movement.UnitCost.Value)
                        : InboundCostResult.Failure(StockAdjustmentErrors.UnitCostRequired());
                }

            case ItemMovementType.OpeningBalance:
                {
                    var unitCost = await dbContext.StockOpeningBalanceLines
                        .AsNoTracking()
                        .Where(line =>
                            line.CompanyId == companyId &&
                            line.StockOpeningBalanceId == movement.ReferenceId &&
                            line.ItemId == movement.ItemId)
                        .Select(line => (decimal?)line.Price)
                        .SingleOrDefaultAsync(cancellationToken);

                    return unitCost.HasValue
                        ? InboundCostResult.Success(unitCost.Value)
                        : movement.UnitCost.HasValue
                            ? InboundCostResult.Success(movement.UnitCost.Value)
                        : InboundCostResult.Failure(MissingSourceError(movement));
                }

            case ItemMovementType.TransferIn:
                return InboundCostResult.Success(
                    movement.UnitCost ?? 0m);

            default:
                return InboundCostResult.Failure(InvalidInboundMovementType());
        }
    }

    private async Task<InvoiceLine?> FindInvoiceLineAsync(
        ItemMovement movement,
        CancellationToken cancellationToken) =>
        await dbContext.InvoiceLines
            .AsNoTracking()
            .SingleOrDefaultAsync(
                line =>
                    line.CompanyId == companyId &&
                    line.InvoiceId == movement.ReferenceId &&
                    line.ItemId == movement.ItemId,
                cancellationToken);

    private async Task<decimal?> ResolvePurchaseUnitCostAsync(
        ItemMovement movement,
        CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.Id == movement.ReferenceId)
            .Select(invoice => new
            {
                invoice.BaseTotal,
                invoice.BaseSubtotal,
                invoice.BaseDiscountAmount
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var lines = await dbContext.InvoiceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.InvoiceId == movement.ReferenceId &&
                !line.IsDeleted &&
                line.ItemId.HasValue)
            .OrderBy(line => line.Id)
            .Select(line => new
            {
                line.Id,
                line.ItemId,
                line.Quantity,
                line.BaseTotal
            })
            .ToListAsync(cancellationToken);
        if (lines.Count == 0 || lines.Sum(line => line.Quantity) <= 0m)
        {
            return null;
        }

        var linesSubtotal = InventoryCostRules.RoundValue(
            lines.Sum(line => line.BaseTotal));
        var totalSubtotal = invoice.BaseSubtotal > 0m
            ? invoice.BaseSubtotal
            : linesSubtotal;
        var itemDiscount = totalSubtotal <= 0m
            ? 0m
            : InventoryCostRules.RoundValue(
                invoice.BaseDiscountAmount * linesSubtotal / totalSubtotal);
        var targetTotal = InventoryCostRules.RoundValue(
            linesSubtotal - itemDiscount);
        var allocated = 0m;
        var itemTotal = 0m;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var amount = index == lines.Count - 1
                ? targetTotal - allocated
                : linesSubtotal <= 0m
                    ? 0m
                    : InventoryCostRules.RoundValue(
                        targetTotal * line.BaseTotal / linesSubtotal);
            allocated = InventoryCostRules.RoundValue(allocated + amount);
            if (line.ItemId == movement.ItemId)
            {
                itemTotal = InventoryCostRules.RoundValue(itemTotal + amount);
            }
        }

        var itemQuantity = lines
            .Where(line => line.ItemId == movement.ItemId)
            .Sum(line => line.Quantity);
        return itemQuantity <= 0m
            ? null
            : InventoryCostRules.RoundUnitCost(itemTotal / itemQuantity);
    }

    private void ProcessInbound(
        ItemMovement movement,
        decimal unitCost,
        Queue<PendingOutbound> pending,
        ICollection<InventoryCostAllocation> allocations,
        ref decimal quantity,
        ref decimal averageCost,
        ref decimal inventoryValue)
    {
        unitCost = InventoryCostRules.RoundUnitCost(unitCost);
        var inboundQuantity =
            InventoryCostRules.RoundQuantity(movement.QuantityIn);
        var quantityBefore = quantity;
        var valueBefore = inventoryValue;
        var availableToAllocate = inboundQuantity;

        while (availableToAllocate > 0m && pending.Count > 0)
        {
            var pendingOutbound = pending.Peek();
            var allocatedQuantity = Math.Min(
                availableToAllocate,
                pendingOutbound.RemainingQuantity);
            allocatedQuantity =
                InventoryCostRules.RoundQuantity(allocatedQuantity);

            allocations.Add(
                InventoryCostAllocation.Create(
                    companyId,
                    movement.StoreId,
                    movement.ItemId,
                    pendingOutbound.Movement.Id,
                    movement.Id,
                    allocatedQuantity,
                    unitCost,
                    timeProvider.GetUtcNow().UtcDateTime));

            pendingOutbound.Allocate(allocatedQuantity, unitCost);
            availableToAllocate = InventoryCostRules.RoundQuantity(
                availableToAllocate - allocatedQuantity);

            pendingOutbound.ApplyUpdatedSnapshot();
            if (pendingOutbound.RemainingQuantity == 0m)
            {
                pending.Dequeue();
            }
        }

        quantity = InventoryCostRules.RoundQuantity(
            quantityBefore + inboundQuantity);

        if (quantity <= 0m)
        {
            averageCost = 0m;
            inventoryValue = 0m;
        }
        else if (quantityBefore <= 0m)
        {
            inventoryValue = InventoryCostRules.CalculateTotal(
                availableToAllocate,
                unitCost);
            averageCost = InventoryCostRules.CalculateAverage(
                inventoryValue,
                quantity);
        }
        else
        {
            inventoryValue = InventoryCostRules.RoundValue(
                valueBefore +
                InventoryCostRules.CalculateTotal(
                    inboundQuantity,
                    unitCost));
            averageCost = InventoryCostRules.CalculateAverage(
                inventoryValue,
                quantity);
        }

        movement.ApplyCostSnapshot(
            InventoryCostStatus.Final,
            0m,
            unitCost,
            InventoryCostRules.CalculateTotal(
                inboundQuantity,
                unitCost),
            quantity,
            averageCost,
            inventoryValue);
    }

    private static void ProcessPendingInbound(
        ItemMovement movement,
        ref decimal quantity,
        ref decimal averageCost,
        ref decimal inventoryValue)
    {
        var inboundQuantity =
            InventoryCostRules.RoundQuantity(movement.QuantityIn);
        var quantityBefore = quantity;
        quantity = InventoryCostRules.RoundQuantity(
            quantityBefore + inboundQuantity);

        if (quantity <= 0m || quantityBefore <= 0m)
        {
            averageCost = 0m;
            inventoryValue = 0m;
        }
        else
        {
            averageCost = InventoryCostRules.CalculateAverage(
                inventoryValue,
                quantity);
        }

        movement.ApplyCostSnapshot(
            InventoryCostStatus.Pending,
            inboundQuantity,
            unitCost: null,
            totalCost: 0m,
            quantityAfter: quantity,
            averageCostAfter: averageCost,
            inventoryValueAfter: inventoryValue);
    }

    private static IReadOnlyDictionary<int, decimal>
        BuildSalesReturnSourceCostOverrides(
            IReadOnlyCollection<PendingSalesReturn> pendingSalesReturns)
    {
        var overrides = new Dictionary<int, decimal>();

        foreach (var group in pendingSalesReturns.GroupBy(
                     pendingReturn => pendingReturn.SourceMovement.Id))
        {
            var sourceMovement = group.First().SourceMovement;
            if (sourceMovement.UnitCost.HasValue &&
                sourceMovement.CostStatus is
                    InventoryCostStatus.Final or
                    InventoryCostStatus.Revalued)
            {
                overrides[sourceMovement.Id] =
                    sourceMovement.UnitCost.Value;
                continue;
            }

            var returnedQuantity = InventoryCostRules.RoundQuantity(
                group.Sum(pendingReturn =>
                    pendingReturn.Movement.QuantityIn));
            if (sourceMovement.PendingCostQuantity > returnedQuantity)
            {
                continue;
            }

            var costedQuantity = InventoryCostRules.RoundQuantity(
                sourceMovement.QuantityOut -
                sourceMovement.PendingCostQuantity);
            if (costedQuantity <= 0m)
            {
                continue;
            }

            overrides[sourceMovement.Id] =
                InventoryCostRules.CalculateAverage(
                    sourceMovement.TotalCost,
                    costedQuantity);
        }

        return overrides;
    }

    private static void ProcessOutbound(
        ItemMovement movement,
        Queue<PendingOutbound> pending,
        ref decimal quantity,
        ref decimal averageCost,
        ref decimal inventoryValue)
    {
        var outboundQuantity =
            InventoryCostRules.RoundQuantity(movement.QuantityOut);
        var quantityBefore = quantity;
        var averageBefore = averageCost;
        var coveredQuantity = Math.Min(
            outboundQuantity,
            Math.Max(quantityBefore, 0m));
        coveredQuantity =
            InventoryCostRules.RoundQuantity(coveredQuantity);
        var pendingQuantity = InventoryCostRules.RoundQuantity(
            outboundQuantity - coveredQuantity);
        var coveredCost = InventoryCostRules.CalculateTotal(
            coveredQuantity,
            averageBefore);

        quantity = InventoryCostRules.RoundQuantity(
            quantityBefore - outboundQuantity);

        if (quantity <= 0m)
        {
            averageCost = 0m;
            inventoryValue = 0m;
        }
        else
        {
            averageCost = averageBefore;
            inventoryValue = InventoryCostRules.RoundValue(
                inventoryValue - coveredCost);
        }

        var status = pendingQuantity == 0m
            ? InventoryCostStatus.Final
            : coveredQuantity == 0m
                ? InventoryCostStatus.Pending
                : InventoryCostStatus.PartiallyCosted;
        decimal? unitCost = pendingQuantity == 0m
            ? averageBefore
            : null;

        movement.ApplyCostSnapshot(
            status,
            pendingQuantity,
            unitCost,
            coveredCost,
            quantity,
            averageCost,
            inventoryValue);

        if (pendingQuantity > 0m)
        {
            pending.Enqueue(
                new PendingOutbound(
                    movement,
                    pendingQuantity,
                    coveredCost));
        }
    }

    private static bool ComesBefore(
        ItemMovement candidate,
        ItemMovement movement) =>
        candidate.MovementDate < movement.MovementDate ||
        candidate.MovementDate == movement.MovementDate &&
        (candidate.CreatedOn < movement.CreatedOn ||
         candidate.CreatedOn == movement.CreatedOn &&
         candidate.Id < movement.Id);

    private sealed class PendingOutbound(
        ItemMovement movement,
        decimal remainingQuantity,
        decimal accumulatedCost)
    {
        public ItemMovement Movement { get; } = movement;

        public decimal RemainingQuantity { get; private set; } =
            remainingQuantity;

        public decimal AccumulatedCost { get; private set; } =
            accumulatedCost;

        public void Allocate(decimal quantity, decimal unitCost)
        {
            RemainingQuantity = InventoryCostRules.RoundQuantity(
                RemainingQuantity - quantity);
            AccumulatedCost = InventoryCostRules.RoundValue(
                AccumulatedCost +
                InventoryCostRules.CalculateTotal(quantity, unitCost));
        }

        public void ApplyUpdatedSnapshot()
        {
            var status = RemainingQuantity == 0m
                ? InventoryCostStatus.Revalued
                : InventoryCostStatus.PartiallyCosted;
            decimal? unitCost = RemainingQuantity == 0m
                ? InventoryCostRules.CalculateAverage(
                    AccumulatedCost,
                    Movement.QuantityOut)
                : null;

            Movement.ApplyCostSnapshot(
                status,
                RemainingQuantity,
                unitCost,
                AccumulatedCost,
                Movement.QuantityAfter,
                Movement.AverageCostAfter,
                Movement.InventoryValueAfter);
        }
    }

    private sealed record InboundCostResult(
        decimal UnitCost,
        Error? Error,
        ItemMovement? PendingSourceMovement)
    {
        public static InboundCostResult Success(decimal unitCost) =>
            new(
                UnitCost: unitCost,
                Error: null,
                PendingSourceMovement: null);

        public static InboundCostResult Failure(Error error) =>
            new(
                UnitCost: 0m,
                Error: error,
                PendingSourceMovement: null);

        public static InboundCostResult Pending(
            ItemMovement sourceMovement) =>
            new(
                UnitCost: 0m,
                Error: null,
                PendingSourceMovement: sourceMovement);
    }

    private sealed record PendingSalesReturn(
        ItemMovement Movement,
        ItemMovement SourceMovement);

    private sealed record TimelineReplayResult(
        decimal Quantity,
        decimal AverageCost,
        decimal InventoryValue,
        IReadOnlyCollection<PendingSalesReturn> PendingSalesReturns,
        Error? Error)
    {
        public static TimelineReplayResult Success(
            decimal Quantity,
            decimal AverageCost,
            decimal InventoryValue,
            IReadOnlyCollection<PendingSalesReturn> PendingSalesReturns) =>
            new(
                Quantity: Quantity,
                AverageCost: AverageCost,
                InventoryValue: InventoryValue,
                PendingSalesReturns: PendingSalesReturns,
                Error: null);

        public static TimelineReplayResult Failure(Error error) =>
            new(
                Quantity: 0m,
                AverageCost: 0m,
                InventoryValue: 0m,
                PendingSalesReturns: [],
                Error: error);
    }
}
