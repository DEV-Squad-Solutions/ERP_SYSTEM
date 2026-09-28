using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.FiscalYears.FiscalYearErrors;
using static MiniErp.Application.Features.Invoices.InvoiceErrors;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed class InvoicePostingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IAccountMappingResolver accountMappingResolver,
    IAutomaticPostingService automaticPostingService)
    : IInvoicePostingService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<AutomaticJournalEntryResult>> SynchronizeAsync(
        int invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.Invoices
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == invoiceId)
            .Select(entity => new
            {
                entity.Id,
                entity.InvoiceNumber,
                entity.InvoiceDate,
                entity.InvoiceType,
                entity.BusinessPartnerId,
                entity.Currency,
                entity.Total,
                entity.ExchangeRate,
                entity.BaseSubtotal,
                entity.BaseDiscountAmount,
                entity.BaseTotal,
                entity.Notes
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (invoice is null)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                InvoiceErrors.NotFound(invoiceId));
        }

        var invoiceAmount = invoice.BaseTotal > 0m
            ? invoice.BaseTotal
            : ExchangeRateRules.ConvertToBase(
                invoice.Total,
                invoice.ExchangeRate);

        var invoiceLines = await dbContext.InvoiceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.InvoiceId == invoice.Id)
            .Select(line => new
            {
                line.ItemId,
                line.BaseTotal
            })
            .ToListAsync(cancellationToken);
        var hasItemLines = invoiceLines.Any(line => line.ItemId.HasValue);
        var hasServiceLines = invoiceLines.Any(line => !line.ItemId.HasValue);
        var lineBaseSubtotal = InventoryCostRules.RoundValue(
            invoiceLines.Sum(line => line.BaseTotal));
        var itemBaseSubtotal = InventoryCostRules.RoundValue(
            invoiceLines
                .Where(line => line.ItemId.HasValue)
                .Sum(line => line.BaseTotal));
        var serviceBaseSubtotal = InventoryCostRules.RoundValue(
            invoiceLines
                .Where(line => !line.ItemId.HasValue)
                .Sum(line => line.BaseTotal));
        var baseSubtotal = invoice.BaseSubtotal > 0m
            ? invoice.BaseSubtotal
            : lineBaseSubtotal;
        var itemAmount = AllocateNetAmount(
            itemBaseSubtotal,
            baseSubtotal,
            invoice.BaseDiscountAmount);
        var serviceAmount = AllocateNetAmount(
            serviceBaseSubtotal,
            baseSubtotal,
            invoice.BaseDiscountAmount);
        var allocatedNetAmount = InventoryCostRules.RoundValue(
            itemAmount + serviceAmount);
        if (allocatedNetAmount != invoiceAmount)
        {
            if (hasServiceLines)
            {
                serviceAmount = InventoryCostRules.RoundValue(
                    serviceAmount + invoiceAmount - allocatedNetAmount);
            }
            else
            {
                itemAmount = InventoryCostRules.RoundValue(
                    itemAmount + invoiceAmount - allocatedNetAmount);
            }
        }

        var hasItemMovements = await dbContext.ItemMovements
            .AsNoTracking()
            .AnyAsync(
                movement =>
                    movement.CompanyId == companyId &&
                    movement.ReferenceId == invoice.Id &&
                    (movement.MovementType == ItemMovementType.Sales ||
                     movement.MovementType == ItemMovementType.SalesReturn ||
                     movement.MovementType == ItemMovementType.Purchase ||
                     movement.MovementType == ItemMovementType.PurchaseReturn),
                cancellationToken);
        if (!hasItemLines && hasItemMovements && invoiceLines.Count == 0)
        {
            itemAmount = invoiceAmount;
            serviceAmount = 0m;
        }
        var hasUnresolvedPurchaseReturnCost = hasItemMovements &&
            invoice.InvoiceType == InvoiceType.PurchaseReturn &&
            await dbContext.ItemMovements
                .AsNoTracking()
                .AnyAsync(
                    movement =>
                        movement.CompanyId == companyId &&
                        movement.ReferenceId == invoice.Id &&
                        movement.MovementType ==
                            ItemMovementType.PurchaseReturn &&
                        (movement.CostStatus ==
                            InventoryCostStatus.Pending ||
                         movement.CostStatus ==
                            InventoryCostStatus.PartiallyCosted),
                    cancellationToken);

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.StartDate <= invoice.InvoiceDate &&
                year.EndDate >= invoice.InvoiceDate)
            .Select(year => new
            {
                year.Id,
                year.Name,
                year.Status
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (fiscalYear is null)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                DateNotCovered(invoice.InvoiceDate, "InvoiceDate"));
        }

        if (fiscalYear.Status != FiscalYearStatus.Open)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                Closed(invoice.InvoiceDate, fiscalYear.Name, "InvoiceDate"));
        }

        if (hasUnresolvedPurchaseReturnCost)
        {
            var deleteResult = await automaticPostingService.DeleteAsync(
                JournalEntrySourceType.Invoice,
                invoice.Id,
                cancellationToken);
            if (deleteResult.IsFailure)
            {
                return Result<AutomaticJournalEntryResult>.Failure(
                    deleteResult.Errors);
            }

            return Result<AutomaticJournalEntryResult>.Success(
                new AutomaticJournalEntryResult(
                    JournalEntryId: 0,
                    EntryNumber: string.Empty,
                    Created: false));
        }

        var mappingTypes = GetInvoiceMappings(invoice.InvoiceType);
        var controlAccountResult = await accountMappingResolver.ResolveAsync(
            fiscalYear.Id,
            mappingTypes.Control,
            cancellationToken: cancellationToken);
        if (controlAccountResult.IsFailure)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                controlAccountResult.Errors);
        }

        var lines = new List<JournalEntryLineRequest>();
        var transactionInvoiceAmount =
            ExchangeRateRules.IsValidRate(invoice.ExchangeRate)
                ? ExchangeRateRules.ConvertFromBase(
                    invoiceAmount,
                    invoice.ExchangeRate)
                : invoice.Total;
        if (invoice.InvoiceType == InvoiceType.PurchaseReturn)
        {
            var itemAccountId = 0;
            if (itemAmount > 0m)
            {
                var itemAccountResult = await accountMappingResolver.ResolveAsync(
                    fiscalYear.Id,
                    (hasItemLines || hasItemMovements)
                        ? AccountingMappingType.Inventory
                        : mappingTypes.Invoice,
                    cancellationToken: cancellationToken);
                if (itemAccountResult.IsFailure)
                {
                    return Result<AutomaticJournalEntryResult>.Failure(
                        itemAccountResult.Errors);
                }

                itemAccountId = itemAccountResult.Value;
            }

            var serviceAccountId = 0;
            if (serviceAmount > 0m)
            {
                var serviceAccountResult = await accountMappingResolver.ResolveAsync(
                    fiscalYear.Id,
                    AccountingMappingType.ServicePurchaseReturn,
                    cancellationToken: cancellationToken);
                if (serviceAccountResult.IsFailure)
                {
                    return Result<AutomaticJournalEntryResult>.Failure(
                        serviceAccountResult.Errors);
                }

                serviceAccountId = serviceAccountResult.Value;
            }

            var carryingCost = hasItemMovements
                ? await dbContext.ItemMovements
                    .AsNoTracking()
                    .Where(movement =>
                        movement.CompanyId == companyId &&
                        movement.ReferenceId == invoice.Id &&
                        movement.MovementType == ItemMovementType.PurchaseReturn)
                    .SumAsync(
                        movement => (decimal?)movement.TotalCost,
                        cancellationToken) ?? 0m
                : 0m;
            var adjustmentAccountId = 0;
            var itemDifference = itemAmount - carryingCost;
            if (itemDifference != 0m)
            {
                var adjustmentResult = await accountMappingResolver.ResolveAsync(
                    fiscalYear.Id,
                    itemDifference > 0m
                        ? AccountingMappingType.InventoryAdjustmentGain
                        : AccountingMappingType.InventoryAdjustmentLoss,
                    cancellationToken: cancellationToken);
                if (adjustmentResult.IsFailure)
                {
                    return Result<AutomaticJournalEntryResult>.Failure(
                        adjustmentResult.Errors);
                }

                adjustmentAccountId = adjustmentResult.Value;
            }

            AddPurchaseReturnItemLines(
                lines,
                supplierAccountId: controlAccountResult.Value,
                inventoryAccountId: itemAccountId,
                carryingCost: carryingCost,
                itemAmount: itemAmount,
                invoiceAmount: invoiceAmount,
                currency: invoice.Currency,
                exchangeRate: invoice.ExchangeRate,
                transactionInvoiceAmount: transactionInvoiceAmount,
                invoiceNumber: invoice.InvoiceNumber,
                businessPartnerId: invoice.BusinessPartnerId,
                adjustmentAccountId: adjustmentAccountId,
                serviceAccountId: serviceAccountId,
                serviceAmount: serviceAmount);
        }
        else
        {
            var allocations = new List<InvoicePostingAllocation>();
            if (itemAmount > 0m)
            {
                var itemAccountResult = await accountMappingResolver.ResolveAsync(
                    fiscalYear.Id,
                    (hasItemLines || hasItemMovements) && invoice.InvoiceType is
                        InvoiceType.Purchase
                        ? AccountingMappingType.Inventory
                        : mappingTypes.Invoice,
                    cancellationToken: cancellationToken);
                if (itemAccountResult.IsFailure)
                {
                    return Result<AutomaticJournalEntryResult>.Failure(
                        itemAccountResult.Errors);
                }

                allocations.Add(new InvoicePostingAllocation(
                    AccountId: itemAccountResult.Value,
                    Amount: itemAmount,
                    Description: "الأصناف"));
            }

            if (serviceAmount > 0m)
            {
                var serviceAccountResult = await accountMappingResolver.ResolveAsync(
                    fiscalYear.Id,
                    invoice.InvoiceType switch
                    {
                        InvoiceType.Sales => AccountingMappingType.ServiceSales,
                        InvoiceType.SalesReturn =>
                            AccountingMappingType.ServiceSalesReturn,
                        _ => AccountingMappingType.ServicePurchase
                    },
                    cancellationToken: cancellationToken);
                if (serviceAccountResult.IsFailure)
                {
                    return Result<AutomaticJournalEntryResult>.Failure(
                        serviceAccountResult.Errors);
                }

                allocations.Add(new InvoicePostingAllocation(
                    AccountId: serviceAccountResult.Value,
                    Amount: serviceAmount,
                    Description: "الخدمات"));
            }

            AddInvoiceAmountLines(
                lines: lines,
                invoiceType: invoice.InvoiceType,
                allocations: allocations,
                controlAccountId: controlAccountResult.Value,
                currency: invoice.Currency,
                exchangeRate: invoice.ExchangeRate,
                totalAmount: invoiceAmount,
                transactionTotalAmount: transactionInvoiceAmount,
                invoiceNumber: invoice.InvoiceNumber,
                businessPartnerId: invoice.BusinessPartnerId);
        }

        var cost = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.ReferenceId == invoice.Id &&
                (movement.MovementType == ItemMovementType.Sales ||
                 movement.MovementType == ItemMovementType.SalesReturn))
            .SumAsync(
                movement => (decimal?)movement.TotalCost,
                cancellationToken) ?? 0m;
        if (cost > 0m)
        {
            var inventoryAccountResult = await accountMappingResolver.ResolveAsync(
                fiscalYear.Id,
                AccountingMappingType.Inventory,
                cancellationToken: cancellationToken);
            if (inventoryAccountResult.IsFailure)
            {
                return Result<AutomaticJournalEntryResult>.Failure(
                    inventoryAccountResult.Errors);
            }

            var costAccountResult = await accountMappingResolver.ResolveAsync(
                fiscalYear.Id,
                AccountingMappingType.CostOfGoodsSold,
                cancellationToken: cancellationToken);
            if (costAccountResult.IsFailure)
            {
                return Result<AutomaticJournalEntryResult>.Failure(
                    costAccountResult.Errors);
            }

            var isSales = invoice.InvoiceType == InvoiceType.Sales;
            lines.Add(new JournalEntryLineRequest(
                AccountId: costAccountResult.Value,
                Description: $"تكلفة الفاتورة {invoice.InvoiceNumber}",
                Debit: isSales ? cost : 0m,
                Credit: isSales ? 0m : cost));
            lines.Add(new JournalEntryLineRequest(
                AccountId: inventoryAccountResult.Value,
                Description: $"تكلفة الفاتورة {invoice.InvoiceNumber}",
                Debit: isSales ? 0m : cost,
                Credit: isSales ? cost : 0m));
        }

        var payments = await dbContext.InvoicePayments
            .AsNoTracking()
            .Where(payment =>
                payment.CompanyId == companyId &&
                payment.InvoiceId == invoice.Id)
            .Select(payment => new
            {
                payment.CashVoucher.CashboxId,
                payment.CashVoucher.Direction,
                payment.InvoiceCurrency,
                payment.AppliedAmount,
                payment.CashboxCurrency,
                payment.CashboxAmount,
                payment.InvoiceToBaseRate,
                payment.CashboxToBaseRate,
                payment.AppliedBaseAmount,
                payment.CashboxBaseAmount
            })
            .ToListAsync(cancellationToken);
        foreach (var payment in payments)
        {
            if (!payment.CashboxId.HasValue)
            {
                return Result<AutomaticJournalEntryResult>.Failure(
                    PostingCashboxRequired());
            }

            var cashboxAccountResult = await accountMappingResolver.ResolveAsync(
                fiscalYear.Id,
                AccountingMappingType.Cashbox,
                payment.CashboxId.Value,
                cancellationToken);
            if (cashboxAccountResult.IsFailure)
            {
                return Result<AutomaticJournalEntryResult>.Failure(
                    cashboxAccountResult.Errors);
            }

            AddPaymentLines(
                lines,
                payment.Direction,
                cashboxAccountResult.Value,
                controlAccountResult.Value,
                payment.CashboxBaseAmount,
                payment.AppliedBaseAmount,
                payment.CashboxCurrency,
                payment.CashboxToBaseRate,
                payment.CashboxAmount,
                payment.InvoiceCurrency,
                payment.InvoiceToBaseRate,
                payment.AppliedAmount,
                invoice.InvoiceNumber,
                invoice.BusinessPartnerId,
                ToJournalPartyType(invoice.InvoiceType),
                payment.CashboxId.Value);

            var paymentBalance = payment.Direction == CashDirection.Receipt
                ? payment.CashboxBaseAmount - payment.AppliedBaseAmount
                : payment.AppliedBaseAmount - payment.CashboxBaseAmount;
            var roundedDifference = ExchangeRateRules.RoundBaseAmount(
                paymentBalance);
            if (roundedDifference > 0m)
            {
                var gainResult = await accountMappingResolver.ResolveAsync(
                    fiscalYear.Id,
                    AccountingMappingType.ExchangeGain,
                    cancellationToken: cancellationToken);
                if (gainResult.IsFailure)
                {
                    return Result<AutomaticJournalEntryResult>.Failure(
                        gainResult.Errors);
                }

                lines.Add(new JournalEntryLineRequest(
                    AccountId: gainResult.Value,
                    Description: $"ربح فرق عملة للفاتورة {invoice.InvoiceNumber}",
                    Debit: 0m,
                    Credit: roundedDifference));
            }
            else if (roundedDifference < 0m)
            {
                var lossResult = await accountMappingResolver.ResolveAsync(
                    fiscalYear.Id,
                    AccountingMappingType.ExchangeLoss,
                    cancellationToken: cancellationToken);
                if (lossResult.IsFailure)
                {
                    return Result<AutomaticJournalEntryResult>.Failure(
                        lossResult.Errors);
                }

                lines.Add(new JournalEntryLineRequest(
                    AccountId: lossResult.Value,
                    Description: $"خسارة فرق عملة للفاتورة {invoice.InvoiceNumber}",
                    Debit: Math.Abs(roundedDifference),
                    Credit: 0m));
            }
        }

        var hasEffectiveLines = lines.Any(line =>
            line.Debit > 0m || line.Credit > 0m);
        if (!hasEffectiveLines)
        {
            var deleteResult = await automaticPostingService.DeleteAsync(
                JournalEntrySourceType.Invoice,
                invoice.Id,
                cancellationToken);
            if (deleteResult.IsFailure)
            {
                return Result<AutomaticJournalEntryResult>.Failure(
                    deleteResult.Errors);
            }

            return Result<AutomaticJournalEntryResult>.Success(
                new AutomaticJournalEntryResult(
                    JournalEntryId: 0,
                    EntryNumber: string.Empty,
                    Created: false));
        }

        return await automaticPostingService.CreateOrUpdateAsync(
            new AutomaticJournalEntryRequest(
                FiscalYearId: fiscalYear.Id,
                EntryDate: invoice.InvoiceDate,
                Description: $"فاتورة {GetInvoiceTypeName(invoice.InvoiceType)} " +
                    invoice.InvoiceNumber,
                SourceType: JournalEntrySourceType.Invoice,
                SourceId: invoice.Id,
                SourceNumber: invoice.InvoiceNumber,
                Lines: lines),
            cancellationToken);
    }

    public Task<Result> DeleteAsync(
        int invoiceId,
        CancellationToken cancellationToken = default) =>
        automaticPostingService.DeleteAsync(
            JournalEntrySourceType.Invoice,
            invoiceId,
            cancellationToken);

    private static (
        AccountingMappingType Invoice,
        AccountingMappingType Control) GetInvoiceMappings(
        InvoiceType invoiceType) =>
        invoiceType switch
        {
            InvoiceType.Sales => (
                AccountingMappingType.Sales,
                AccountingMappingType.CustomerControl),
            InvoiceType.SalesReturn => (
                AccountingMappingType.SalesReturn,
                AccountingMappingType.CustomerControl),
            InvoiceType.Purchase => (
                AccountingMappingType.Purchase,
                AccountingMappingType.SupplierControl),
            InvoiceType.PurchaseReturn => (
                AccountingMappingType.PurchaseReturn,
                AccountingMappingType.SupplierControl),
            _ => throw new ArgumentOutOfRangeException(nameof(invoiceType))
        };

    private static void AddInvoiceAmountLines(
        ICollection<JournalEntryLineRequest> lines,
        InvoiceType invoiceType,
        IReadOnlyList<InvoicePostingAllocation> allocations,
        int controlAccountId,
        CurrencyCode currency,
        decimal exchangeRate,
        decimal totalAmount,
        decimal transactionTotalAmount,
        string invoiceNumber,
        int businessPartnerId)
    {
        var invoiceSideIsDebit = invoiceType is
            InvoiceType.Purchase or InvoiceType.SalesReturn;
        var allocatedTransactionAmount = 0m;
        for (var index = 0; index < allocations.Count; index++)
        {
            var allocation = allocations[index];
            var transactionAmount = index == allocations.Count - 1
                ? transactionTotalAmount - allocatedTransactionAmount
                : ExchangeRateRules.ConvertFromBase(
                    allocation.Amount,
                    exchangeRate);
            allocatedTransactionAmount += transactionAmount;
            lines.Add(new JournalEntryLineRequest(
                AccountId: allocation.AccountId,
                Description: $"{allocation.Description} للفاتورة {invoiceNumber}",
                Debit: invoiceSideIsDebit ? allocation.Amount : 0m,
                Credit: invoiceSideIsDebit ? 0m : allocation.Amount,
                Currency: currency,
                ExchangeRate: exchangeRate,
                TransactionDebit: invoiceSideIsDebit ? transactionAmount : 0m,
                TransactionCredit: invoiceSideIsDebit ? 0m : transactionAmount));
        }

        lines.Add(new JournalEntryLineRequest(
            AccountId: controlAccountId,
            Description: $"طرف الفاتورة {invoiceNumber}",
            Debit: invoiceSideIsDebit ? 0m : totalAmount,
            Credit: invoiceSideIsDebit ? totalAmount : 0m,
            PartyType: ToJournalPartyType(invoiceType),
            PartyId: businessPartnerId,
            Currency: currency,
            ExchangeRate: exchangeRate,
            TransactionDebit: invoiceSideIsDebit ? 0m : transactionTotalAmount,
            TransactionCredit: invoiceSideIsDebit ? transactionTotalAmount : 0m));
    }

    private static void AddPurchaseReturnItemLines(
        ICollection<JournalEntryLineRequest> lines,
        int supplierAccountId,
        int inventoryAccountId,
        decimal carryingCost,
        decimal itemAmount,
        decimal invoiceAmount,
        CurrencyCode currency,
        decimal exchangeRate,
        decimal transactionInvoiceAmount,
        string invoiceNumber,
        int businessPartnerId,
        int adjustmentAccountId,
        int serviceAccountId,
        decimal serviceAmount)
    {
        lines.Add(new JournalEntryLineRequest(
            AccountId: supplierAccountId,
            Description: $"طرف مرتجع الشراء {invoiceNumber}",
            Debit: invoiceAmount,
            Credit: 0m,
            PartyType: JournalPartyType.Supplier,
            PartyId: businessPartnerId,
            Currency: currency,
            ExchangeRate: exchangeRate,
            TransactionDebit: transactionInvoiceAmount,
            TransactionCredit: 0m));
        if (carryingCost > 0m && inventoryAccountId > 0)
        {
            lines.Add(new JournalEntryLineRequest(
                AccountId: inventoryAccountId,
                Description: $"تكلفة مخزون مرتجع الشراء {invoiceNumber}",
                Debit: 0m,
                Credit: carryingCost));
        }

        var difference = itemAmount - carryingCost;
        if (difference > 0m && adjustmentAccountId > 0)
        {
            lines.Add(new JournalEntryLineRequest(
                AccountId: adjustmentAccountId,
                Description: $"فرق تكلفة مرتجع الشراء {invoiceNumber}",
                Debit: 0m,
                Credit: difference));
        }
        else if (difference < 0m && adjustmentAccountId > 0)
        {
            lines.Add(new JournalEntryLineRequest(
                AccountId: adjustmentAccountId,
                Description: $"فرق تكلفة مرتجع الشراء {invoiceNumber}",
                Debit: Math.Abs(difference),
                Credit: 0m));
        }

        if (serviceAmount > 0m && serviceAccountId > 0)
        {
            var transactionServiceAmount = ExchangeRateRules.ConvertFromBase(
                serviceAmount,
                exchangeRate);
            lines.Add(new JournalEntryLineRequest(
                AccountId: serviceAccountId,
                Description: $"خدمات مرتجع الشراء {invoiceNumber}",
                Debit: 0m,
                Credit: serviceAmount,
                Currency: currency,
                ExchangeRate: exchangeRate,
                TransactionDebit: 0m,
                TransactionCredit: transactionServiceAmount));
        }
    }

    private static decimal AllocateNetAmount(
        decimal lineSubtotal,
        decimal invoiceSubtotal,
        decimal invoiceDiscount) =>
        lineSubtotal <= 0m
            ? 0m
            : invoiceSubtotal <= 0m
                ? lineSubtotal
                : InventoryCostRules.RoundValue(
                    lineSubtotal -
                    InventoryCostRules.RoundValue(
                        invoiceDiscount * lineSubtotal / invoiceSubtotal));

    private sealed record InvoicePostingAllocation(
        int AccountId,
        decimal Amount,
        string Description);

    private static void AddPaymentLines(
        ICollection<JournalEntryLineRequest> lines,
        CashDirection direction,
        int cashboxAccountId,
        int controlAccountId,
        decimal cashboxBaseAmount,
        decimal appliedBaseAmount,
        CurrencyCode cashboxCurrency,
        decimal cashboxExchangeRate,
        decimal cashboxAmount,
        CurrencyCode invoiceCurrency,
        decimal invoiceExchangeRate,
        decimal appliedAmount,
        string invoiceNumber,
        int businessPartnerId,
        JournalPartyType partyType,
        int cashboxId)
    {
        var isReceipt = direction == CashDirection.Receipt;
        var originalCashboxAmount = cashboxAmount > 0m
            ? cashboxAmount
            : ExchangeRateRules.ConvertFromBase(
                cashboxBaseAmount,
                cashboxExchangeRate);
        var originalAppliedAmount = appliedAmount > 0m
            ? appliedAmount
            : ExchangeRateRules.ConvertFromBase(
                appliedBaseAmount,
                invoiceExchangeRate);
        lines.Add(new JournalEntryLineRequest(
            AccountId: cashboxAccountId,
            Description: $"سداد الفاتورة {invoiceNumber}",
            Debit: isReceipt ? cashboxBaseAmount : 0m,
            Credit: isReceipt ? 0m : cashboxBaseAmount,
            PartyType: JournalPartyType.Cashbox,
            PartyId: cashboxId,
            Currency: cashboxCurrency,
            ExchangeRate: cashboxExchangeRate,
            TransactionDebit: isReceipt ? originalCashboxAmount : 0m,
            TransactionCredit: isReceipt ? 0m : originalCashboxAmount));
        lines.Add(new JournalEntryLineRequest(
            AccountId: controlAccountId,
            Description: $"تسوية سداد الفاتورة {invoiceNumber}",
            Debit: isReceipt ? 0m : appliedBaseAmount,
            Credit: isReceipt ? appliedBaseAmount : 0m,
            PartyType: partyType,
            PartyId: businessPartnerId,
            Currency: invoiceCurrency,
            ExchangeRate: invoiceExchangeRate,
            TransactionDebit: isReceipt ? 0m : originalAppliedAmount,
            TransactionCredit: isReceipt ? originalAppliedAmount : 0m));
    }

    private static JournalPartyType ToJournalPartyType(
        InvoiceType invoiceType) => invoiceType is
        InvoiceType.Sales or InvoiceType.SalesReturn
            ? JournalPartyType.Customer
            : JournalPartyType.Supplier;

    private static string GetInvoiceTypeName(InvoiceType invoiceType) =>
        invoiceType switch
        {
            InvoiceType.Sales => "بيع",
            InvoiceType.Purchase => "شراء",
            InvoiceType.SalesReturn => "مرتجع بيع",
            InvoiceType.PurchaseReturn => "مرتجع شراء",
            _ => invoiceType.ToString()
        };
}
