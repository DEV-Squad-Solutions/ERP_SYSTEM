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

            var currentResult = await fiscalYearPeriodGuard.EnsureOpenForFiscalYearAsync(
                movement.FiscalYearId,
                movement.MovementDate,
                "movementDate",
                cancellationToken);
            if (currentResult.IsFailure)
            {
                return currentResult.Error;
            }

            if (original.MovementDate != movement.MovementDate)
            {
                var originalFiscalYearId = await ResolveFiscalYearIdAsync(
                    original.MovementDate,
                    cancellationToken);
                if (!originalFiscalYearId.HasValue)
                {
                    return FiscalYearErrors.DateNotCovered(original.MovementDate, "movementDate");
                }
                var originalResult = await fiscalYearPeriodGuard.EnsureOpenForFiscalYearAsync(
                    originalFiscalYearId.Value,
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
                    // Generated carry documents display a money-scale price, but
                    // carry movements retain the previous year's precise cost.
                    if (movement.UnitCost.HasValue &&
                        StockOpeningBalanceCarryForwardRules.IsCarriedForward(
                            movement.ReferenceNumber, notes: null))
                    {
                        return InboundCostResult.Success(movement.UnitCost.Value);
                    }

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
}
