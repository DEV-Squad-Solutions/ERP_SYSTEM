using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.FiscalYears;

public sealed class FiscalYearInventoryCarryForwardService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IFiscalYearInventoryCarryForwardService
{
    private const string MarkerPrefix = "AUTO_FY_INVENTORY_CARRY:";
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result> CarryForwardAsync(
        int sourceFiscalYearId,
        int targetFiscalYearId,
        DateOnly targetStartDate,
        string sourceFiscalYearName,
        CancellationToken cancellationToken = default)
    {
        var movementRows = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.FiscalYearId == sourceFiscalYearId)
            .Select(movement => new
            {
                movement.Id,
                movement.StoreId,
                movement.ItemId,
                movement.ItemUnitId,
                movement.MovementDate,
                movement.CreatedOn,
                movement.QuantityIn,
                movement.QuantityOut,
                movement.AverageCostAfter
            })
            .ToListAsync(cancellationToken);

        var legacyOpeningRows = await dbContext.StockOpeningBalanceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.StockOpeningBalance.CompanyId == companyId &&
                line.StockOpeningBalance.FiscalYearId ==
                    sourceFiscalYearId &&
                !dbContext.ItemMovements.Any(movement =>
                    movement.CompanyId == companyId &&
                    movement.FiscalYearId == sourceFiscalYearId &&
                    movement.StoreId == line.StockOpeningBalance.StoreId &&
                    movement.ItemId == line.ItemId &&
                    movement.MovementType == ItemMovementType.OpeningBalance &&
                    movement.ReferenceId == line.StockOpeningBalanceId))
            .Select(line => new LegacyOpeningRow(
                line.StockOpeningBalance.StoreId,
                line.ItemId,
                line.ItemUnitId,
                line.Quantity,
                line.Price))
            .ToListAsync(cancellationToken);

        var movementsByKey = movementRows.ToLookup(movement =>
            (movement.StoreId, movement.ItemId));
        var legacyByKey = legacyOpeningRows.ToLookup(line =>
            (line.StoreId, line.ItemId));
        var keys = movementRows
            .Select(movement => (movement.StoreId, movement.ItemId))
            .Concat(legacyOpeningRows.Select(line =>
                (line.StoreId, line.ItemId)))
            .Distinct()
            .ToArray();
        var closingRows = keys
            .Select(key =>
            {
                var movements = movementsByKey[key].ToArray();
                var legacyRows = legacyByKey[key].ToArray();
                var latest = movements
                    .OrderByDescending(movement => movement.MovementDate)
                    .ThenByDescending(movement => movement.CreatedOn)
                    .ThenByDescending(movement => movement.Id)
                    .FirstOrDefault();
                var legacyQuantity = legacyRows.Sum(line => line.Quantity);
                var legacyValue = legacyRows.Sum(line =>
                    line.Quantity * line.Price);
                var quantity = InventoryCostRules.RoundQuantity(
                    movements.Sum(movement =>
                        movement.QuantityIn - movement.QuantityOut) +
                    legacyQuantity);
                var averageCost = latest is not null
                    ? latest.AverageCostAfter
                    : legacyQuantity == 0m
                        ? 0m
                        : legacyValue / legacyQuantity;
                return new ClosingInventoryRow(
                    StoreId: key.StoreId,
                    ItemId: key.ItemId,
                    ItemUnitId: latest?.ItemUnitId ??
                        legacyRows.Select(line => line.ItemUnitId).FirstOrDefault(),
                    Quantity: quantity,
                    AverageCost: InventoryCostRules.RoundUnitCost(
                        averageCost));
            })
            .ToArray();

        var negative = closingRows.FirstOrDefault(row => row.Quantity < 0m);
        if (negative is not null)
        {
            return Result.Failure(
                Error.Conflict(
                    "FiscalYears.NegativeInventoryClosingBalance",
                    $"لا يمكن إقفال السنة لأن رصيد الصنف رقم {negative.ItemId} بالمخزن رقم {negative.StoreId} سالب.",
                    "inventory"));
        }

        var marker = $"{MarkerPrefix}{sourceFiscalYearId}";
        var existingDocuments = await dbContext.StockOpeningBalances
            .Include(balance => balance.Lines)
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.FiscalYearId == targetFiscalYearId &&
                balance.Notes == marker)
            .ToListAsync(cancellationToken);
        var existingByStore = existingDocuments.ToDictionary(
            balance => balance.StoreId);
        var rowsByStore = closingRows
            .Where(row => row.Quantity > 0m)
            .GroupBy(row => row.StoreId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var stale in existingDocuments.Where(document =>
                     !rowsByStore.ContainsKey(document.StoreId)))
        {
            await RemoveDocumentAsync(stale, cancellationToken);
        }

        foreach (var (storeId, rows) in rowsByStore)
        {
            if (!existingByStore.TryGetValue(storeId, out var document))
            {
                document = new StockOpeningBalance
                {
                    CompanyId = companyId,
                    FiscalYearId = targetFiscalYearId,
                    StoreId = storeId,
                    DocumentNumber = BuildDocumentNumber(
                        sourceFiscalYearId,
                        storeId),
                    DocumentDate = targetStartDate,
                    Notes = marker
                };
                dbContext.StockOpeningBalances.Add(document);
                existingByStore[storeId] = document;
            }
            else
            {
                document.DocumentDate = targetStartDate;
                document.Notes = marker;
                dbContext.StockOpeningBalanceLines.RemoveRange(
                    document.Lines);
                document.Lines.Clear();
            }

            foreach (var row in rows)
            {
                var line = new StockOpeningBalanceLine
                {
                    CompanyId = companyId,
                    ItemId = row.ItemId,
                    ItemUnitId = row.ItemUnitId,
                    Count = 1,
                    Weight = row.Quantity,
                    Price = decimal.Round(
                        row.AverageCost,
                        StockOpeningBalanceAmountRules.MoneyScale,
                        MidpointRounding.AwayFromZero),
                    Notes = $"مرحل من السنة المالية {sourceFiscalYearName}",
                    StockOpeningBalance = document
                };
                line.CalculateAmounts();
                document.Lines.Add(line);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var document in existingByStore.Values.Where(document =>
                     rowsByStore.ContainsKey(document.StoreId)))
        {
            var oldMovements = await dbContext.ItemMovements
                .Where(movement =>
                    movement.CompanyId == companyId &&
                    movement.FiscalYearId == targetFiscalYearId &&
                    movement.MovementType == ItemMovementType.OpeningBalance &&
                    movement.ReferenceId == document.Id &&
                    movement.ReferenceNumber == document.DocumentNumber)
                .ToListAsync(cancellationToken);
            dbContext.ItemMovements.RemoveRange(oldMovements);

            foreach (var line in document.Lines)
            {
                var movement = new ItemMovement
                {
                    CompanyId = companyId,
                    FiscalYearId = targetFiscalYearId,
                    StoreId = document.StoreId,
                    ItemId = line.ItemId,
                    ItemUnitId = line.ItemUnitId,
                    MovementType = ItemMovementType.OpeningBalance,
                    ReferenceId = document.Id,
                    ReferenceNumber = document.DocumentNumber,
                    MovementDate = targetStartDate,
                    QuantityIn = line.Quantity,
                    QuantityOut = 0m,
                    Description =
                        $"رصيد افتتاحي مرحل من السنة المالية {sourceFiscalYearName}"
                };
                movement.ApplyCostSnapshot(
                    costStatus: InventoryCostStatus.Final,
                    pendingCostQuantity: 0m,
                    unitCost: line.Price,
                    totalCost: line.Total,
                    quantityAfter: line.Quantity,
                    averageCostAfter: line.Price,
                    inventoryValueAfter: line.Total);
                dbContext.ItemMovements.Add(movement);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task RemoveDocumentAsync(
        StockOpeningBalance document,
        CancellationToken cancellationToken)
    {
        var movements = await dbContext.ItemMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.MovementType == ItemMovementType.OpeningBalance &&
                movement.ReferenceId == document.Id &&
                movement.ReferenceNumber == document.DocumentNumber)
            .ToListAsync(cancellationToken);
        dbContext.ItemMovements.RemoveRange(movements);
        dbContext.StockOpeningBalanceLines.RemoveRange(document.Lines);
        dbContext.StockOpeningBalances.Remove(document);
    }

    private static string BuildDocumentNumber(
        int sourceFiscalYearId,
        int storeId) =>
        $"FYOB-{sourceFiscalYearId}-{storeId}";

    private sealed record ClosingInventoryRow(
        int StoreId,
        int ItemId,
        int? ItemUnitId,
        decimal Quantity,
        decimal AverageCost);

    private sealed record LegacyOpeningRow(
        int StoreId,
        int ItemId,
        int? ItemUnitId,
        decimal Quantity,
        decimal Price);
}
