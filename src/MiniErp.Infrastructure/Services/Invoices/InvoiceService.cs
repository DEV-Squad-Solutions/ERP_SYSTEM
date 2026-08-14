using System.Data;
using static MiniErp.Application.Features.Invoices.InvoiceErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IInvoiceQueryService invoiceQueryService,
    IExchangeRateResolver exchangeRateResolver,
    IInvoiceInventoryService invoiceInventoryService,
    TimeProvider timeProvider)
    : IInvoiceService, IScopedService
{
    private static readonly ItemMovementType[] InvoiceItemMovementTypes =
    [
        ItemMovementType.Sales,
        ItemMovementType.SalesReturn,
        ItemMovementType.Purchase,
        ItemMovementType.PurchaseReturn
    ];

    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<InvoiceResponse>> AddAsync(
        InvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var invoice = request.Adapt<Invoice>();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        await invoiceInventoryService.LockCostingAsync(
            request.Lines
                .Where(line => line.ItemId.HasValue)
                .Select(line => new InventoryCostingKey(
                    request.StoreId,
                    line.ItemId!.Value))
                .Distinct()
                .ToArray(),
            cancellationToken);

        var preparation = await PrepareAsync(
            invoice,
            request.Lines,
            request.ContainerLines,
            currentInvoiceId: null,
            currentInvoiceNumber: null,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<InvoiceResponse>.Failure(preparation.Error);
        }

        invoice.CompanyId = companyId;
        invoice.Currency = preparation.Value.Currency;
        ApplyPreparedReturnDiscount(invoice, preparation.Value);

        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            invoice.Currency,
            invoice.InvoiceDate,
            request.ExchangeRate,
            cancellationToken);
        if (exchangeRateResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(
                exchangeRateResult.Error);
        }

        AddLines(invoice, request, preparation.Value);
        AddContainerLines(invoice, request);
        invoice.CalculateTotal();
        invoice.ApplyExchangeRate(
            exchangeRateResult.Value.ExchangeRateId,
            exchangeRateResult.Value.Rate);
        var amountError = ValidateAmounts(
            invoice,
            requestedWBTotal: request.WBTotal,
            requireCalculatedWBTotalMatch: false);
        if (amountError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(amountError);
        }

        var paymentPreparation = await PreparePaymentAsync(
            invoice,
            request.CashboxId,
            request.CashboxExchangeRate,
            currentInvoiceId: null,
            cancellationToken);
        if (paymentPreparation.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(
                paymentPreparation.Error);
        }

        invoice.Touch(timeProvider.GetUtcNow().UtcDateTime);

        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SaveSideEffectsAsync(
            invoice,
            paymentPreparation.Value,
            cancellationToken);

        var costingError = await invoiceInventoryService.RecalculateCostingAsync(
            GetCostingKeys(invoice),
            cancellationToken);
        if (costingError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(costingError);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var responseResult = await invoiceQueryService.GetByIdAsync(
            invoice.Id,
            cancellationToken);
        if (responseResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(responseResult.Error);
        }

        await transaction.CommitAsync(cancellationToken);
        return responseResult;
    }

    public async Task<Result<InvoiceResponse>> UpdateAsync(
        int id,
        InvoiceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<InvoiceResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<InvoiceResponse>.Failure(RowVersionRequired());
        }

        var requestedValues = request.Adapt<Invoice>();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var invoice = await LoadForWriteAsync(id, cancellationToken);
        if (invoice is null)
        {
            return Result<InvoiceResponse>.Failure(NotFound(id));
        }

        if (!invoice.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<InvoiceResponse>.Failure(Concurrency());
        }

        if (await HasActiveLinkedReturnsAsync(
                invoice.Lines.Select(line => line.Id).ToArray(),
                cancellationToken))
        {
            return Result<InvoiceResponse>.Failure(LinkedSalesReturnsExist());
        }

        if (await HasCashVoucherTripReferencesAsync(id, cancellationToken))
        {
            return Result<InvoiceResponse>.Failure(
                DriverTripHasCashVouchers());
        }

        var oldItemMovements = await LoadItemMovementsAsync(
            id,
            cancellationToken);
        var oldCostingKeys = GetCostingKeys(oldItemMovements);
        await invoiceInventoryService.LockCostingAsync(
            oldCostingKeys
                .Concat(request.Lines
                    .Where(line => line.ItemId.HasValue)
                    .Select(line =>
                        new InventoryCostingKey(
                            request.StoreId,
                            line.ItemId!.Value)))
                .Distinct()
                .ToArray(),
            cancellationToken);

        var entry = dbContext.Entry(invoice);
        entry.Property(item => item.RowVersion).OriginalValue = request.RowVersion;

        var preparation = await PrepareAsync(
            requestedValues,
            request.Lines,
            request.ContainerLines,
            currentInvoiceId: id,
            currentInvoiceNumber: invoice.InvoiceNumber,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<InvoiceResponse>.Failure(preparation.Error);
        }

        request.Adapt(invoice);
        NormalizeDriverValues(invoice);
        invoice.Currency = preparation.Value.Currency;
        ApplyPreparedReturnDiscount(invoice, preparation.Value);

        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            invoice.Currency,
            invoice.InvoiceDate,
            request.ExchangeRate,
            cancellationToken);
        if (exchangeRateResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(
                exchangeRateResult.Error);
        }

        ReplaceLines(invoice, request, preparation.Value);
        ReplaceContainerLines(invoice, request);
        invoice.CalculateTotal();
        invoice.ApplyExchangeRate(
            exchangeRateResult.Value.ExchangeRateId,
            exchangeRateResult.Value.Rate);
        var amountError = ValidateAmounts(
            invoice,
            requestedWBTotal: null,
            requireCalculatedWBTotalMatch: true);
        if (amountError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(amountError);
        }

        var paymentPreparation = await PreparePaymentAsync(
            invoice,
            request.CashboxId,
            request.CashboxExchangeRate,
            currentInvoiceId: id,
            cancellationToken);
        if (paymentPreparation.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(
                paymentPreparation.Error);
        }

        invoice.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(item => item.LastModifiedAt).IsModified = true;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            await RemoveSideEffectsAsync(
                invoice,
                removeItemMovements: false,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await SaveSideEffectsAsync(
                invoice,
                paymentPreparation.Value,
                cancellationToken);

            var costingError = await invoiceInventoryService.RecalculateCostingAsync(
                oldCostingKeys
                    .Concat(GetCostingKeys(invoice))
                    .Distinct()
                    .ToArray(),
                cancellationToken);
            if (costingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<InvoiceResponse>.Failure(costingError);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(Concurrency());
        }

        var responseResult = await invoiceQueryService.GetByIdAsync(
            id,
            cancellationToken);
        if (responseResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<InvoiceResponse>.Failure(responseResult.Error);
        }

        await transaction.CommitAsync(cancellationToken);
        return responseResult;
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

        var invoice = await LoadForWriteAsync(id, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (!invoice.RowVersion.SequenceEqual(rowVersion))
        {
            return Result.Failure(Concurrency());
        }

        if (await HasCashVoucherTripReferencesAsync(id, cancellationToken))
        {
            return Result.Failure(DriverTripHasCashVouchers());
        }

        if (await HasActiveLinkedReturnsAsync(
                invoice.Lines.Select(line => line.Id).ToArray(),
                cancellationToken))
        {
            return Result.Failure(LinkedSalesReturnsExist());
        }

        var oldItemMovements = await LoadItemMovementsAsync(
            id,
            cancellationToken);
        var costingKeys = GetCostingKeys(oldItemMovements);
        await invoiceInventoryService.LockCostingAsync(
            costingKeys,
            cancellationToken);

        var stockError = await ValidateStockAsync(
            invoice,
            [],
            currentInvoiceId: invoice.Id,
            currentInvoiceNumber: invoice.InvoiceNumber,
            cancellationToken);
        if (stockError is not null)
        {
            return Result.Failure(stockError);
        }

        var paymentError = await ValidatePaymentRemovalAsync(
            invoice.Id,
            cancellationToken);
        if (paymentError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(paymentError);
        }

        await RemoveSideEffectsAsync(
            invoice,
            removeItemMovements: true,
            cancellationToken);

        var entry = dbContext.Entry(invoice);
        entry.Property(item => item.RowVersion).OriginalValue = rowVersion;

        dbContext.InvoiceLines.RemoveRange(invoice.Lines);
        dbContext.InvoiceContainerLines.RemoveRange(invoice.ContainerLines);
        dbContext.Invoices.Remove(invoice);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            var costingError = await invoiceInventoryService.RecalculateCostingAsync(
                costingKeys,
                cancellationToken);
            if (costingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result.Failure(costingError);
            }

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
}
