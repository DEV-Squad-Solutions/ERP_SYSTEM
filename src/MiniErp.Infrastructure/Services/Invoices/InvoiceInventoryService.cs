using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.Items;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.Invoices.InvoiceErrors;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed class InvoiceInventoryService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IInventoryStockService inventoryStockService,
    IInventoryCostingService inventoryCostingService): IInvoiceInventoryService, IScopedService
{
    private static readonly ItemMovementType[] InvoiceItemMovementTypes =
    [
        ItemMovementType.Sales,
        ItemMovementType.SalesReturn,
        ItemMovementType.Purchase,
        ItemMovementType.PurchaseReturn
    ];

    private readonly int companyId = currentCompanyContext.CompanyId;

    public Task LockCostingAsync(
        IReadOnlyCollection<InventoryCostingKey> keys,
        CancellationToken cancellationToken = default) =>
        inventoryCostingService.LockAsync(keys, cancellationToken);

    public Task<Error?> RecalculateCostingAsync(
        IReadOnlyCollection<InventoryCostingKey> keys,
        CancellationToken cancellationToken = default) =>
        inventoryCostingService.RecalculateAsync(keys, cancellationToken);

    public async Task<Error?> ValidateStockAsync(
        int storeId,
        DateOnly invoiceDate,
        InvoiceType invoiceType,
        IReadOnlyList<InvoiceLineRequest> lines,
        int? currentInvoiceId,
        string? currentInvoiceNumber,
        CancellationToken cancellationToken = default)
    {
        var hasCurrentInvoiceId = currentInvoiceId.HasValue;
        var hasCurrentInvoiceNumber =
            !string.IsNullOrWhiteSpace(currentInvoiceNumber);
        if (hasCurrentInvoiceId != hasCurrentInvoiceNumber)
        {
            return InvalidCurrentInvoiceReference();
        }

        var stockLines = new List<InventoryStockLine>(lines.Count);
        foreach (var line in lines)
        {
            if (!line.ItemId.HasValue)
            {
                continue;
            }

            if (!InvoiceLineValues.TryGetEffective(
                    line,
                    out var count,
                    out var weight))
            {
                return InvalidCalculatedAmounts(
                    InvoiceCalculationErrorKind.Quantity);
            }

            if (!InvoiceAmountRules.TryCalculate(
                    count,
                    weight,
                    price: 0m,
                    out var quantity,
                    out _))
            {
                return InvalidCalculatedAmounts(
                    InvoiceCalculationErrorKind.Quantity);
            }

            stockLines.Add(new InventoryStockLine(
                ItemId: line.ItemId.Value,
                Quantity: quantity));
        }

        var replacedMovement = currentInvoiceId is int invoiceId
            ? new InventoryMovementReference(
                MovementTypes: InvoiceItemMovementTypes,
                ReferenceId: invoiceId,
                ReferenceNumber: currentInvoiceNumber!)
            : null;

        return await inventoryStockService.ValidateTimelineAsync(
            new InventoryStockProposal(
                StoreId: storeId,
                MovementDate: invoiceDate,
                IsInbound: InvoiceMovementRules.IsInbound(invoiceType),
                Lines: stockLines,
                ReplacedMovement: replacedMovement,
                OperationDescription: currentInvoiceId.HasValue
                    ? $"تعديل الفاتورة {currentInvoiceNumber}"
                    : "إضافة الفاتورة",
                ErrorFieldName: nameof(InvoiceRequest.Lines)),
            cancellationToken);
    }

    public async Task<Result<InvoiceItemBalanceResponse>> GetItemBalanceAsync(
        int storeId,
        int itemId,
        DateOnly asOfDate,
        int? invoiceId = null,
        CancellationToken cancellationToken = default)
    {
        if (storeId <= 0)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                ItemBalanceStoreInvalid());
        }

        if (itemId <= 0)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                ItemBalanceItemInvalid());
        }

        if (asOfDate == DateOnly.MinValue)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                ItemBalanceDateRequired());
        }

        if (invoiceId is <= 0)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                ItemBalanceInvoiceInvalid());
        }

        var store = await dbContext.Stores
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == storeId)
            .Select(candidate => new
            {
                candidate.Name,
                candidate.IsActive,
                candidate.IsContainerStore
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (store is null)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                StoreNotFound(storeId));
        }

        if (!store.IsActive)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(StoreInactive());
        }

        if (store.IsContainerStore)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                ContainerStoreNotAllowed());
        }

        var item = await dbContext.Items
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == itemId)
            .Select(candidate => new
            {
                candidate.Name,
                candidate.ItemUnitId,
                ItemUnitName = candidate.ItemUnit.Name,
                candidate.IsActive,
                ItemUnitIsActive = candidate.ItemUnit.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                ItemNotFound([itemId]));
        }

        if (!item.IsActive)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                ItemInactive([itemId]));
        }

        if (!item.ItemUnitIsActive)
        {
            return Result<InvoiceItemBalanceResponse>.Failure(
                ItemUnitInactive([itemId]));
        }

        string? excludedInvoiceNumber = null;
        if (invoiceId is int currentInvoiceId)
        {
            excludedInvoiceNumber = await dbContext.Invoices
                .AsNoTracking()
                .Where(candidate =>
                    candidate.CompanyId == companyId &&
                    candidate.Id == currentInvoiceId)
                .Select(candidate => candidate.InvoiceNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (excludedInvoiceNumber is null)
            {
                return Result<InvoiceItemBalanceResponse>.Failure(
                    NotFound(currentInvoiceId));
            }
        }

        var excludedMovement = invoiceId is int excludedInvoiceId
            ? new InventoryMovementReference(
                MovementTypes: InvoiceItemMovementTypes,
                ReferenceId: excludedInvoiceId,
                ReferenceNumber: excludedInvoiceNumber!)
            : null;
        var balances = await inventoryStockService.GetBalancesAsync(
            storeId,
            [itemId],
            asOfDate,
            excludedMovement,
            cancellationToken);
        var costSnapshots = await inventoryCostingService.GetSnapshotsAsync(
            storeId,
            [itemId],
            asOfDate,
            cancellationToken);
        var costSnapshot = costSnapshots[itemId];

        var pricingExpenses = await dbContext.ItemPricingExpenses
            .AsNoTracking()
            .Where(expense =>
                expense.CompanyId == companyId &&
                expense.ItemId == itemId)
            .OrderBy(expense => expense.Id)
            .Select(expense => new
            {
                expense.Id,
                expense.Name,
                expense.Amount,
                expense.Notes
            })
            .ToListAsync(cancellationToken);

        return Result<InvoiceItemBalanceResponse>.Success(
            new InvoiceItemBalanceResponse(
                StoreId: storeId,
                StoreName: store.Name,
                ItemId: itemId,
                ItemName: item.Name,
                ItemUnitId: item.ItemUnitId,
                ItemUnitName: item.ItemUnitName,
                AsOfDate: asOfDate,
                Balance: balances[itemId],
                AverageCost: costSnapshot.AverageCost,
                InventoryValue: costSnapshot.InventoryValue)
            {
                PricingExpenses = pricingExpenses
                    .Select(expense => new ItemPricingExpenseResponse(
                        Id: expense.Id,
                        Name: expense.Name,
                        Amount: expense.Amount,
                        Notes: expense.Notes))
                    .ToArray()
            });
    }
}
