using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.FiscalYears;

public sealed class FiscalYearInventoryCarryForwardService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IInventoryCostingService inventoryCostingService,
    IInventoryStockService inventoryStockService,
    IInventoryPostingService? inventoryPostingService = null)
    : IFiscalYearInventoryCarryForwardService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result> CarryForwardAsync(
        int sourceFiscalYearId,
        int targetFiscalYearId,
        DateOnly targetStartDate,
        string sourceFiscalYearName,
        CancellationToken cancellationToken = default)
    {
        await using var ownedTransaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

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

        var marker = $"{StockOpeningBalanceCarryForwardRules.MarkerPrefix}{sourceFiscalYearId}";
        var documentPrefix = $"{StockOpeningBalanceCarryForwardRules.DocumentNumberPrefix}{sourceFiscalYearId}-";
        var existingDocuments = await dbContext.StockOpeningBalances
            .Include(balance => balance.Lines)
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.FiscalYearId == targetFiscalYearId &&
                ((balance.Notes != null && balance.Notes.ToUpper() == marker) ||
                 balance.DocumentNumber.ToUpper().StartsWith(documentPrefix)))
            .ToListAsync(cancellationToken);
        var existingByStore = existingDocuments.ToDictionary(
            balance => balance.StoreId);
        var rowsByStore = closingRows
            .Where(row => row.Quantity > 0m)
            .GroupBy(row => row.StoreId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var affectedKeys = closingRows
            .Select(row => new InventoryCostingKey(row.StoreId, row.ItemId, targetFiscalYearId))
            .Concat(existingDocuments.SelectMany(document => document.Lines.Select(line =>
                new InventoryCostingKey(document.StoreId, line.ItemId, targetFiscalYearId))))
            .Distinct()
            .ToArray();
        await inventoryCostingService.LockAsync(affectedKeys, cancellationToken);

        foreach (var document in existingDocuments)
        {
            var replacementRows = rowsByStore.GetValueOrDefault(document.StoreId) ?? [];
            var stockError = await inventoryStockService.ValidateTimelineAsync(
                new InventoryStockProposal(
                    StoreId: document.StoreId,
                    MovementDate: targetStartDate,
                    IsInbound: true,
                    Lines: replacementRows.Select(row => new InventoryStockLine(
                        ItemId: row.ItemId, Quantity: row.Quantity)).ToArray(),
                    ReplacedMovement: new InventoryMovementReference(
                        MovementTypes: [ItemMovementType.OpeningBalance],
                        ReferenceId: document.Id,
                        ReferenceNumber: document.DocumentNumber),
                    OperationDescription: "تحديث الرصيد الافتتاحي المرحل عند إعادة إقفال السنة",
                    ErrorFieldName: "inventory"),
                cancellationToken);
            if (stockError is not null)
            {
                return Result.Failure(stockError);
            }
        }

        foreach (var stale in existingDocuments.Where(document =>
                     !rowsByStore.ContainsKey(document.StoreId)))
        {
            if (inventoryPostingService is not null)
            {
                var cleanup = await inventoryPostingService.DeleteAsync(
                    JournalEntrySourceType.StockOpeningBalance, stale.Id, cancellationToken);
                if (cleanup.IsFailure)
                {
                    if (ownedTransaction is not null)
                    {
                        await ownedTransaction.RollbackAsync(cancellationToken);
                        dbContext.ChangeTracker.Clear();
                    }
                    return Result.Failure(cleanup.Errors);
                }
            }
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
                    DocumentNumber = StockOpeningBalanceCarryForwardRules.BuildDocumentNumber(
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
            var linesByItem = document.Lines.ToDictionary(line => line.ItemId);
            var staleMovements = oldMovements.Where(movement =>
                !linesByItem.ContainsKey(movement.ItemId)).ToArray();
            await RemoveMovementsAsync(staleMovements, cancellationToken);
            var movementsByItem = oldMovements.Except(staleMovements)
                .ToDictionary(movement => movement.ItemId);

            foreach (var line in document.Lines)
            {
                if (!movementsByItem.TryGetValue(line.ItemId, out var movement))
                {
                    movement = new ItemMovement
                    {
                        CompanyId = companyId,
                        FiscalYearId = targetFiscalYearId,
                        StoreId = document.StoreId,
                        ItemId = line.ItemId,
                        MovementType = ItemMovementType.OpeningBalance,
                        ReferenceId = document.Id,
                        ReferenceNumber = document.DocumentNumber
                    };
                    dbContext.ItemMovements.Add(movement);
                }
                movement.ItemUnitId = line.ItemUnitId;
                movement.MovementDate = targetStartDate;
                movement.QuantityIn = line.Quantity;
                movement.QuantityOut = 0m;
                movement.Description = $"رصيد افتتاحي مرحل من السنة المالية {sourceFiscalYearName}";
                var row = rowsByStore[document.StoreId].Single(row => row.ItemId == line.ItemId);
                var preciseValue = InventoryCostRules.CalculateTotal(row.Quantity, row.AverageCost);
                movement.ApplyCostSnapshot(
                    costStatus: InventoryCostStatus.Final,
                    pendingCostQuantity: 0m,
                    unitCost: row.AverageCost,
                    totalCost: preciseValue,
                    quantityAfter: row.Quantity,
                    averageCostAfter: row.AverageCost,
                    inventoryValueAfter: preciseValue);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var costingError = await inventoryCostingService.RecalculateAsync(
            affectedKeys, cancellationToken);
        if (costingError is not null)
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            }
            return Result.Failure(costingError);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        if (ownedTransaction is not null)
        {
            await ownedTransaction.CommitAsync(cancellationToken);
        }
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
        await RemoveMovementsAsync(movements, cancellationToken);
        dbContext.StockOpeningBalanceLines.RemoveRange(document.Lines);
        dbContext.StockOpeningBalances.Remove(document);
    }

    private async Task RemoveMovementsAsync(
        IReadOnlyCollection<ItemMovement> movements,
        CancellationToken cancellationToken)
    {
        var ids = movements.Select(movement => movement.Id).ToArray();
        if (ids.Length == 0)
        {
            return;
        }
        var allocations = await dbContext.InventoryCostAllocations
            .Where(allocation => allocation.CompanyId == companyId &&
                (ids.Contains(allocation.InboundMovementId) || ids.Contains(allocation.OutboundMovementId)))
            .ToListAsync(cancellationToken);
        dbContext.InventoryCostAllocations.RemoveRange(allocations);
        dbContext.ItemMovements.RemoveRange(movements);
    }

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
