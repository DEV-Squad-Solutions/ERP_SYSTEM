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
    TimeProvider timeProvider)
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
        var orderedKeys = keys
            .Distinct()
            .OrderBy(key => key.StoreId)
            .ThenBy(key => key.ItemId)
            .ToArray();

        await LockAsync(orderedKeys, cancellationToken);

        foreach (var key in orderedKeys)
        {
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
        }

        return null;
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

        var allocations = await dbContext.InventoryCostAllocations
            .Where(allocation =>
                allocation.CompanyId == companyId &&
                allocation.StoreId == key.StoreId &&
                allocation.ItemId == key.ItemId)
            .ToListAsync(cancellationToken);
        dbContext.InventoryCostAllocations.RemoveRange(allocations);

        var pending = new Queue<PendingOutbound>();
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
                    cancellationToken);
                if (sourceCostResult.Error is not null)
                {
                    return sourceCostResult.Error;
                }

                ProcessInbound(
                    movement,
                    sourceCostResult.UnitCost,
                    pending,
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

        balance.Apply(quantity, averageCost, inventoryValue);
        return null;
    }

    private async Task<InboundCostResult> ResolveInboundUnitCostAsync(
        ItemMovement movement,
        IReadOnlyCollection<ItemMovement> timeline,
        decimal quantityBefore,
        decimal averageCostBefore,
        CancellationToken cancellationToken)
    {
        switch (movement.MovementType)
        {
            case ItemMovementType.Purchase:
                {
                    var line = await FindInvoiceLineAsync(
                        movement,
                        cancellationToken);
                    return line is not null
                        ? InboundCostResult.Success(line.BaseUnitPrice)
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
                            !ComesBefore(sourceMovement, movement) ||
                            sourceMovement.CostStatus is
                                InventoryCostStatus.Pending or
                                InventoryCostStatus.PartiallyCosted ||
                            !sourceMovement.UnitCost.HasValue)
                        {
                            return InboundCostResult.Failure(SalesReturnSourceCostPending());
                        }

                        return InboundCostResult.Success(
                            sourceMovement.UnitCost.Value);
                    }

                    if (quantityBefore > 0m && averageCostBefore > 0m)
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
                return movement.UnitCost.HasValue
                    ? InboundCostResult.Success(movement.UnitCost.Value)
                    : InboundCostResult.Failure(TransferUnitCostRequired());

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

    private void ProcessInbound(
        ItemMovement movement,
        decimal unitCost,
        Queue<PendingOutbound> pending,
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

            dbContext.InventoryCostAllocations.Add(
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
        Error? Error)
    {
        public static InboundCostResult Success(decimal unitCost) =>
            new(unitCost, null);

        public static InboundCostResult Failure(Error error) =>
            new(0m, error);
    }
}
