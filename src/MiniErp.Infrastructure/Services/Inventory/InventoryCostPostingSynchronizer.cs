using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Inventory;

public sealed class InventoryCostPostingSynchronizer(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IInvoicePostingService invoicePostingService,
    IInventoryPostingService inventoryPostingService)
    : IInventoryCostPostingSynchronizer
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Error?> SynchronizeAsync(
        IReadOnlyCollection<InventoryCostingKey> keys,
        CancellationToken cancellationToken = default)
    {
        var distinctKeys = keys.Distinct().ToArray();
        if (distinctKeys.Length == 0)
        {
            return null;
        }

        var storeIds = distinctKeys
            .Select(key => key.StoreId)
            .Distinct()
            .ToArray();
        var itemIds = distinctKeys
            .Select(key => key.ItemId)
            .Distinct()
            .ToArray();
        var keySet = distinctKeys.ToHashSet();

        var movementSources = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                storeIds.Contains(movement.StoreId) &&
                itemIds.Contains(movement.ItemId))
            .Select(movement => new
            {
                movement.StoreId,
                movement.ItemId,
                movement.MovementType,
                movement.ReferenceId
            })
            .ToListAsync(cancellationToken);
        var affectedSources = movementSources
            .Where(source => keySet.Contains(new InventoryCostingKey(
                source.StoreId,
                source.ItemId)))
            .ToArray();

        var invoiceIds = affectedSources
            .Where(source => source.MovementType is
                ItemMovementType.Sales or
                ItemMovementType.SalesReturn or
                ItemMovementType.Purchase or
                ItemMovementType.PurchaseReturn)
            .Select(source => source.ReferenceId)
            .Distinct()
            .Order()
            .ToArray();
        foreach (var invoiceId in invoiceIds)
        {
            var result = await invoicePostingService.SynchronizeAsync(
                invoiceId,
                cancellationToken);
            if (result.IsFailure)
            {
                return result.Error;
            }
        }

        var adjustmentIds = affectedSources
            .Where(source => source.MovementType is
                ItemMovementType.AdjustmentIncrease or
                ItemMovementType.AdjustmentDecrease)
            .Select(source => source.ReferenceId)
            .Distinct()
            .Order()
            .ToArray();
        foreach (var adjustmentId in adjustmentIds)
        {
            var result = await inventoryPostingService
                .SynchronizeStockAdjustmentAsync(
                    adjustmentId,
                    cancellationToken);
            if (result.IsFailure)
            {
                return result.Error;
            }
        }

        var openingBalanceIds = affectedSources
            .Where(source =>
                source.MovementType == ItemMovementType.OpeningBalance)
            .Select(source => source.ReferenceId)
            .Distinct()
            .Order()
            .ToArray();
        foreach (var openingBalanceId in openingBalanceIds)
        {
            var result = await inventoryPostingService
                .SynchronizeStockOpeningBalanceAsync(
                    openingBalanceId,
                    cancellationToken);
            if (result.IsFailure)
            {
                return result.Error;
            }
        }

        return null;
    }
}
