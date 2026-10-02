using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.CashVouchers.CashVoucherErrors;
using static MiniErp.Application.Features.FiscalYears.FiscalYearErrors;

namespace MiniErp.Infrastructure.Services.CashVouchers;

public sealed class CashVoucherPostingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IAccountMappingResolver accountMappingResolver,
    IAutomaticPostingService automaticPostingService)
    : ICashVoucherPostingService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public Task<Result<AutomaticJournalEntryResult>> SynchronizeAsync(
        CashVoucher voucher,
        CancellationToken cancellationToken = default) =>
        SynchronizeAsync(
            voucher,
            partnerReferencesChanged: HasChangedPartnerReferences(voucher),
            cancellationToken: cancellationToken);

    public async Task<Result<AutomaticJournalEntryResult>> SynchronizeAsync(
        CashVoucher voucher,
        bool partnerReferencesChanged,
        CancellationToken cancellationToken = default)
    {
        if (!voucher.IsPosted)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                PostingAccountRequired());
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.StartDate <= voucher.VoucherDate &&
                year.EndDate >= voucher.VoucherDate)
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
                DateNotCovered(
                    voucher.VoucherDate,
                    nameof(CashVoucher.VoucherDate)));
        }

        if (fiscalYear.Status != FiscalYearStatus.Open)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                Closed(
                    voucher.VoucherDate,
                    fiscalYear.Name,
                    nameof(CashVoucher.VoucherDate)));
        }

        if (!voucher.CashboxId.HasValue)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                PostingAccountRequired());
        }

        var cashboxAccountResult = await accountMappingResolver.ResolveAsync(
            fiscalYear.Id,
            AccountingMappingType.Cashbox,
            voucher.CashboxId.Value,
            cancellationToken);
        if (cashboxAccountResult.IsFailure)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                cashboxAccountResult.Errors);
        }

        var partyResult = await ResolveJournalPartyAsync(
            voucher,
            partnerReferencesChanged,
            cancellationToken);
        if (partyResult.IsFailure)
        {
            return Result<AutomaticJournalEntryResult>.Failure(partyResult.Errors);
        }

        var party = partyResult.Value;
        var counterpartResult = await ResolveCounterpartAccountAsync(
            voucher,
            fiscalYear.Id,
            party.PartyType,
            cancellationToken);
        if (counterpartResult.IsFailure)
        {
            return Result<AutomaticJournalEntryResult>.Failure(
                counterpartResult.Errors);
        }

        var amount = voucher.BaseAmount > 0m
            ? voucher.BaseAmount
            : voucher.Amount * voucher.ExchangeRate;
        var transactionAmount = ExchangeRateRules.IsValidRate(voucher.ExchangeRate)
            ? ExchangeRateRules.ConvertFromBase(amount, voucher.ExchangeRate)
            : voucher.Amount;
        var isReceipt = voucher.Direction == CashDirection.Receipt;
        var lines = new List<JournalEntryLineRequest>
        {
            new(
                AccountId: cashboxAccountResult.Value,
                Description: voucher.Description,
                Debit: isReceipt ? amount : 0m,
                Credit: isReceipt ? 0m : amount,
                PartyType: JournalPartyType.Cashbox,
                PartyId: voucher.CashboxId,
                Currency: voucher.Currency,
                ExchangeRate: voucher.ExchangeRate,
                TransactionDebit: isReceipt ? transactionAmount : 0m,
                TransactionCredit: isReceipt ? 0m : transactionAmount),
            new(
                AccountId: counterpartResult.Value,
                Description: voucher.Description,
                Debit: isReceipt ? 0m : amount,
                Credit: isReceipt ? amount : 0m,
                PartyType: party.PartyType,
                PartyId: party.PartyId,
                Currency: voucher.Currency,
                ExchangeRate: voucher.ExchangeRate,
                TransactionDebit: isReceipt ? 0m : transactionAmount,
                TransactionCredit: isReceipt ? transactionAmount : 0m)
        };

        return await automaticPostingService.CreateOrUpdateAsync(
            new AutomaticJournalEntryRequest(
                FiscalYearId: fiscalYear.Id,
                EntryDate: voucher.VoucherDate,
                Description: BuildDescription(voucher),
                SourceType: JournalEntrySourceType.CashVoucher,
                SourceId: voucher.Id,
                SourceNumber: voucher.VoucherNumber,
                Lines: lines),
            cancellationToken);
    }

    public Task<Result> DeleteAsync(
        int voucherId,
        CancellationToken cancellationToken = default) =>
        automaticPostingService.DeleteAsync(
            JournalEntrySourceType.CashVoucher,
            voucherId,
            cancellationToken);

    private async Task<Result<int>> ResolveCounterpartAccountAsync(
        CashVoucher voucher,
        int fiscalYearId,
        JournalPartyType? partyType,
        CancellationToken cancellationToken)
    {
        if (voucher.AccountId.HasValue)
        {
            return Result<int>.Success(voucher.AccountId.Value);
        }

        var mappingType = voucher.PartyType switch
        {
            CashPartyType.Partner when
                partyType == JournalPartyType.Customer =>
                AccountingMappingType.CustomerControl,
            CashPartyType.Partner => AccountingMappingType.SupplierControl,
            CashPartyType.Driver => AccountingMappingType.DriverControl,
            CashPartyType.Employee => AccountingMappingType.EmployeeControl,
            CashPartyType.Other or CashPartyType.None when
                voucher.CashMovementTypeId.HasValue =>
                AccountingMappingType.CashMovementType,
            _ => (AccountingMappingType?)null
        };
        if (!mappingType.HasValue)
        {
            return Result<int>.Failure(PostingAccountRequired());
        }

        var sourceId = mappingType == AccountingMappingType.CashMovementType
            ? voucher.CashMovementTypeId
            : null;
        return await accountMappingResolver.ResolveAsync(
            fiscalYearId,
            mappingType.Value,
            sourceId,
            cancellationToken);
    }

    private static string BuildDescription(CashVoucher voucher) =>
        $"{(voucher.Direction == CashDirection.Receipt ? "سند قبض" : "سند صرف")} " +
        $"{voucher.VoucherNumber}" +
        (string.IsNullOrWhiteSpace(voucher.Description)
            ? string.Empty
            : $" - {voucher.Description.Trim()}");

    private async Task<Result<(JournalPartyType? PartyType, int? PartyId)>>
        ResolveJournalPartyAsync(
            CashVoucher voucher,
            bool partnerReferencesChanged,
            CancellationToken cancellationToken)
    {
        if (voucher.PartyType != CashPartyType.Partner)
        {
            return Result<(JournalPartyType? PartyType, int? PartyId)>.Success(
                voucher.PartyType switch
                {
                    CashPartyType.Employee =>
                        (JournalPartyType.Employee, voucher.EmployeeId),
                    CashPartyType.Driver =>
                        (JournalPartyType.Driver, voucher.DriverId),
                    _ => (null, null)
                });
        }

        // Partners can trade as both customers and suppliers. The document's
        // invoice semantics determine the role, independently of cash direction.
        var movementInvoiceTypes = new List<InvoiceType>();
        if (voucher.CashMovementTypeId is int movementTypeId)
        {
            var movementType = await dbContext.CashMovementTypes
                .AsNoTracking()
                .Where(type => type.CompanyId == companyId && type.Id == movementTypeId)
                .Select(type => new
                {
                    type.Direction,
                    type.IsDefaultForSales,
                    type.IsDefaultForPurchase,
                    type.IsDefaultForSalesReturn,
                    type.IsDefaultForPurchaseReturn
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (movementType is null)
            {
                return Result<(JournalPartyType? PartyType, int? PartyId)>.Failure(
                    MovementTypeNotFound(movementTypeId));
            }

            if (movementType.Direction != voucher.Direction)
            {
                return Result<(JournalPartyType? PartyType, int? PartyId)>.Failure(
                    MovementTypeDirectionMismatch());
            }

            if (movementType.IsDefaultForSales) movementInvoiceTypes.Add(InvoiceType.Sales);
            if (movementType.IsDefaultForPurchase) movementInvoiceTypes.Add(InvoiceType.Purchase);
            if (movementType.IsDefaultForSalesReturn) movementInvoiceTypes.Add(InvoiceType.SalesReturn);
            if (movementType.IsDefaultForPurchaseReturn) movementInvoiceTypes.Add(InvoiceType.PurchaseReturn);
        }

        InvoiceType? linkedInvoiceType = null;
        if (voucher.InvoiceId is int invoiceId)
        {
            var invoice = await dbContext.Invoices
                .AsNoTracking()
                .Where(invoice => invoice.CompanyId == companyId && invoice.Id == invoiceId)
                .Select(invoice => new { invoice.InvoiceType, invoice.BusinessPartnerId })
                .SingleOrDefaultAsync(cancellationToken);
            if (invoice is null || invoice.BusinessPartnerId != voucher.BusinessPartnerId ||
                !Enum.IsDefined(invoice.InvoiceType) ||
                (movementInvoiceTypes.Count > 0 && !movementInvoiceTypes.Contains(invoice.InvoiceType)))
            {
                return Result<(JournalPartyType? PartyType, int? PartyId)>.Failure(
                    PartnerRoleMismatch(nameof(CashVoucher.InvoiceId)));
            }

            linkedInvoiceType = invoice.InvoiceType;
        }

        if (!linkedInvoiceType.HasValue && movementInvoiceTypes.Count > 1)
        {
            return Result<(JournalPartyType? PartyType, int? PartyId)>.Failure(
                PartnerRoleMismatch(nameof(CashVoucher.CashMovementTypeId)));
        }

        var invoiceType = linkedInvoiceType ??
            (movementInvoiceTypes.Count == 1 ? movementInvoiceTypes[0] : (InvoiceType?)null);
        if (invoiceType.HasValue &&
            InvoiceMovementRules.GetPaymentDirection(invoiceType.Value) != voucher.Direction)
        {
            return Result<(JournalPartyType? PartyType, int? PartyId)>.Failure(
                PartnerRoleMismatch(nameof(CashVoucher.Direction)));
        }

        if (!invoiceType.HasValue && !partnerReferencesChanged)
        {
            // Changing which movement is the default clears the old flags.
            // Preserve an already posted role only for the same partner and
            // cash direction; a changed voucher must resolve its role afresh.
            var previousRoles = await dbContext.JournalEntryLines
                .AsNoTracking()
                .Where(line =>
                    line.CompanyId == companyId &&
                    line.JournalEntry.SourceType == JournalEntrySourceType.CashVoucher &&
                    line.JournalEntry.SourceId == voucher.Id &&
                    line.JournalEntry.EntryType == JournalEntryType.Automatic &&
                    line.JournalEntry.Status == JournalEntryStatus.Posted &&
                    line.JournalEntry.ReversalOfEntryId == null &&
                    line.PartyId == voucher.BusinessPartnerId &&
                    (line.PartyType == JournalPartyType.Customer ||
                     line.PartyType == JournalPartyType.Supplier) &&
                    line.JournalEntry.Lines.Any(cashboxLine =>
                        cashboxLine.PartyType == JournalPartyType.Cashbox &&
                        cashboxLine.PartyId == voucher.CashboxId &&
                        (voucher.Direction == CashDirection.Receipt
                            ? cashboxLine.Debit > 0m && cashboxLine.Credit == 0m
                            : cashboxLine.Credit > 0m && cashboxLine.Debit == 0m)))
                .Select(line => line.PartyType)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (previousRoles.Count == 1)
            {
                return Result<(JournalPartyType? PartyType, int? PartyId)>.Success(
                    (previousRoles[0], voucher.BusinessPartnerId));
            }
        }

        var isCustomer = invoiceType.HasValue
            ? invoiceType is InvoiceType.Sales or InvoiceType.SalesReturn
            : voucher.Direction == CashDirection.Receipt;
        return Result<(JournalPartyType? PartyType, int? PartyId)>.Success(
            (isCustomer ? JournalPartyType.Customer : JournalPartyType.Supplier,
             voucher.BusinessPartnerId));
    }

    private bool HasChangedPartnerReferences(CashVoucher voucher)
    {
        var entry = dbContext.Entry(voucher);
        return entry.State != EntityState.Detached &&
            (entry.Property(entity => entity.BusinessPartnerId).OriginalValue != voucher.BusinessPartnerId ||
             entry.Property(entity => entity.CashMovementTypeId).OriginalValue != voucher.CashMovementTypeId ||
             entry.Property(entity => entity.InvoiceId).OriginalValue != voucher.InvoiceId);
    }

    private static Error PartnerRoleMismatch(string fieldName) =>
        Error.Conflict(
            "CashVouchers.PartnerRoleMismatch",
            "نوع الفاتورة ونوع الحركة النقدية واتجاه السند يجب أن تتوافق مع حساب العميل أو المورد.",
            fieldName);
}
