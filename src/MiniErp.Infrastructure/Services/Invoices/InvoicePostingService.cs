using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Companies;
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
                entity.Total,
                entity.ExchangeRate,
                entity.BaseTotal,
                entity.Notes
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (invoice is null)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                InvoiceErrors.NotFound(invoiceId));
        }

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

        var mappingTypes = GetInvoiceMappings(invoice.InvoiceType);
        var invoiceAccountResult = await accountMappingResolver.ResolveAsync(
            fiscalYear.Id,
            mappingTypes.Invoice,
            cancellationToken: cancellationToken);
        if (invoiceAccountResult.IsFailure)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                invoiceAccountResult.Errors);
        }

        var controlAccountResult = await accountMappingResolver.ResolveAsync(
            fiscalYear.Id,
            mappingTypes.Control,
            cancellationToken: cancellationToken);
        if (controlAccountResult.IsFailure)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                controlAccountResult.Errors);
        }

        var invoiceAmount = invoice.BaseTotal > 0m
            ? invoice.BaseTotal
            : ExchangeRateRules.ConvertToBase(
                invoice.Total,
                invoice.ExchangeRate);
        var lines = new List<JournalEntryLineRequest>();
        AddInvoiceAmountLines(
            lines,
            invoice.InvoiceType,
            invoiceAccountResult.Value,
            controlAccountResult.Value,
            invoiceAmount,
            invoice.InvoiceNumber);

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
                invoice.InvoiceNumber);

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
        int invoiceAccountId,
        int controlAccountId,
        decimal amount,
        string invoiceNumber)
    {
        var invoiceSideIsDebit = invoiceType is
            InvoiceType.Purchase or InvoiceType.SalesReturn;
        lines.Add(new JournalEntryLineRequest(
            AccountId: invoiceAccountId,
            Description: $"إجمالي الفاتورة {invoiceNumber}",
            Debit: invoiceSideIsDebit ? amount : 0m,
            Credit: invoiceSideIsDebit ? 0m : amount));
        lines.Add(new JournalEntryLineRequest(
            AccountId: controlAccountId,
            Description: $"طرف الفاتورة {invoiceNumber}",
            Debit: invoiceSideIsDebit ? 0m : amount,
            Credit: invoiceSideIsDebit ? amount : 0m));
    }

    private static void AddPaymentLines(
        ICollection<JournalEntryLineRequest> lines,
        CashDirection direction,
        int cashboxAccountId,
        int controlAccountId,
        decimal cashboxBaseAmount,
        decimal appliedBaseAmount,
        string invoiceNumber)
    {
        var isReceipt = direction == CashDirection.Receipt;
        lines.Add(new JournalEntryLineRequest(
            AccountId: cashboxAccountId,
            Description: $"سداد الفاتورة {invoiceNumber}",
            Debit: isReceipt ? cashboxBaseAmount : 0m,
            Credit: isReceipt ? 0m : cashboxBaseAmount));
        lines.Add(new JournalEntryLineRequest(
            AccountId: controlAccountId,
            Description: $"تسوية سداد الفاتورة {invoiceNumber}",
            Debit: isReceipt ? 0m : appliedBaseAmount,
            Credit: isReceipt ? appliedBaseAmount : 0m));
    }

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
