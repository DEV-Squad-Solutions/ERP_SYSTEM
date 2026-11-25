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
