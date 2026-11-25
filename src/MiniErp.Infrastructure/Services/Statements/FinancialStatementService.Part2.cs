using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.Statements.StatementErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Statements;

public sealed partial class FinancialStatementService
{
    private IQueryable<CashboxStatementRaw> CreateCashboxRows(
        int cashboxId,
        CurrencyCode cashboxCurrency,
        int fiscalYearId)
    {
        var vouchers = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher => voucher.CompanyId == companyId);
        var revaluations = dbContext.CashboxRevaluations
            .AsNoTracking()
            .Where(revaluation => revaluation.CompanyId == companyId);
        var monetaryRevaluations = dbContext.MonetaryAccountRevaluations
            .AsNoTracking()
            .Where(revaluation => revaluation.CompanyId == companyId);

        return
            from line in PostedLedgerLines()
            where line.PartyType == JournalPartyType.Cashbox &&
                  line.PartyId == cashboxId &&
                  line.JournalEntry.FiscalYearId == fiscalYearId
            join voucher in vouchers
                on new
                {
                    line.JournalEntry.SourceId,
                    line.JournalEntry.SourceType
                }
                equals new
                {
                    SourceId = (int?)voucher.Id,
                    SourceType = (JournalEntrySourceType?)
                        JournalEntrySourceType.CashVoucher
                }
                into voucherRows
            from voucher in voucherRows.DefaultIfEmpty()
            join revaluation in revaluations
                on new
                {
                    SourceId = line.JournalEntry.SourceId,
                    SourceType = line.JournalEntry.SourceType
                }
                equals new
                {
                    SourceId = (int?)revaluation.Id,
                    SourceType = (JournalEntrySourceType?)
                        JournalEntrySourceType.CashboxRevaluation
                }
                into revaluationRows
            from revaluation in revaluationRows.DefaultIfEmpty()
            join monetaryRevaluation in monetaryRevaluations
                on new
                {
                    line.JournalEntry.SourceId,
                    line.JournalEntry.SourceType
                }
                equals new
                {
                    SourceId = (int?)monetaryRevaluation.Id,
                    SourceType = (JournalEntrySourceType?)
                        JournalEntrySourceType.MonetaryAccountRevaluation
                }
                into monetaryRevaluationRows
            from monetaryRevaluation in monetaryRevaluationRows.DefaultIfEmpty()
            select new CashboxStatementRaw
            {
                JournalEntryLineId = line.Id,
                JournalEntryId = line.JournalEntryId,
                CashVoucherId = voucher == null ? null : (int?)voucher.Id,
                SourceType = line.JournalEntry.SourceType,
                IsOpening = line.JournalEntry.SourceType ==
                    JournalEntrySourceType.CashboxOpeningBalance ||
                    line.JournalEntry.SourceType ==
                    JournalEntrySourceType.FiscalYearClosing,
                Date = line.JournalEntry.EntryDate,
                CreatedOn = line.JournalEntry.PostedOn,
                DocumentNumber = voucher != null
                    ? voucher.VoucherNumber
                    : line.JournalEntry.SourceNumber ??
                      line.JournalEntry.EntryNumber,
                MovementName = line.JournalEntry.SourceType ==
                    JournalEntrySourceType.CashboxRevaluation ||
                    line.JournalEntry.SourceType ==
                    JournalEntrySourceType.MonetaryAccountRevaluation
                    ? "إعادة تقييم عملة"
                    : voucher != null
                    ? voucher.CashMovementType != null
                        ? voucher.CashMovementType!.Name
                        : voucher.Direction == CashDirection.Receipt
                            ? "سند قبض"
                            : "سند صرف"
                    : line.JournalEntry.SourceType ==
                          JournalEntrySourceType.CashboxTransfer
                            ? line.Debit > 0m
                                ? "تحويل خزائن وارد"
                                : "تحويل خزائن صادر"
                            : line.JournalEntry.EntryType ==
                              JournalEntryType.Adjustment
                                ? "قيد تسوية"
                                : line.JournalEntry.EntryType ==
                                  JournalEntryType.Manual
                                    ? "قيد يدوي"
                                    : line.JournalEntry.EntryType ==
                                      JournalEntryType.Opening
                                        ? "قيد افتتاحي"
                                        : "قيد تلقائي",
                Description = line.Description ??
                    line.JournalEntry.Description,
                PartyName = voucher != null &&
                    voucher.BusinessPartner != null
                        ? voucher.BusinessPartner!.Name
                        : voucher != null && voucher.Driver != null
                            ? voucher.Driver!.Name
                            : voucher != null && voucher.Employee != null
                                ? voucher.Employee!.Name
                                : null,
                ExternalPartyName = voucher == null
                    ? null
                    : voucher.ExternalPartyName,
                CashPartyType = voucher == null
                    ? null
                    : (CashPartyType?)voucher.PartyType,
                BusinessPartnerId = voucher == null
                    ? null
                    : voucher.BusinessPartnerId,
                DriverId = voucher == null ? null : voucher.DriverId,
                DriverTripId = voucher == null ? null : voucher.DriverTripId,
                EmployeeId = voucher == null ? null : voucher.EmployeeId,
                CashMovementTypeId = voucher == null
                    ? null
                    : voucher.CashMovementTypeId,
                Classification = voucher == null
                    ? null
                    : voucher.Classification ??
                      (voucher.CashMovementType == null
                          ? null
                          : voucher.CashMovementType!.Classification),
                Currency = cashboxCurrency,
                ExchangeRate = revaluation != null
                    ? revaluation.ClosingRate
                    : monetaryRevaluation != null
                        ? monetaryRevaluation.ClosingRate
                        : line.ExchangeRate,
                // Revaluations (including base-currency adjustments carried
                // into a new fiscal year) change carrying value, not units.
                ReceiptAmount = line.Currency != cashboxCurrency ||
                    line.JournalEntry.SourceType ==
                    JournalEntrySourceType.CashboxRevaluation
                    ? 0m
                    : line.TransactionDebit,
                PaymentAmount = line.Currency != cashboxCurrency ||
                    line.JournalEntry.SourceType ==
                    JournalEntrySourceType.CashboxRevaluation
                    ? 0m
                    : line.TransactionCredit,
                BaseReceiptAmount = line.Debit,
                BasePaymentAmount = line.Credit,
                ReferenceNumber = voucher == null
                    ? null
                    : voucher.ReferenceNumber
            };
    }

    private IQueryable<PartnerStatementRaw> CreatePartnerRows(
        int partnerId,
        CurrencyCode partnerCurrency,
        int fiscalYearId)
    {
        var invoices = dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.CompanyId == companyId);
        var vouchers = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher => voucher.CompanyId == companyId);
        var monetaryRevaluations = dbContext.MonetaryAccountRevaluations
            .AsNoTracking()
            .Where(revaluation => revaluation.CompanyId == companyId);

        return
            from line in PostedLedgerLines()
            where (line.PartyType == JournalPartyType.Customer ||
                   line.PartyType == JournalPartyType.Supplier) &&
                  line.PartyId == partnerId &&
                  line.JournalEntry.FiscalYearId == fiscalYearId
            join invoice in invoices
                on new
                {
                    line.JournalEntry.SourceId,
                    line.JournalEntry.SourceType
                }
                equals new
                {
                    SourceId = (int?)invoice.Id,
                    SourceType = (JournalEntrySourceType?)
                        JournalEntrySourceType.Invoice
                }
                into invoiceRows
            from invoice in invoiceRows.DefaultIfEmpty()
            join voucher in vouchers
                on new
                {
                    line.JournalEntry.SourceId,
                    line.JournalEntry.SourceType
                }
                equals new
                {
                    SourceId = (int?)voucher.Id,
                    SourceType = (JournalEntrySourceType?)
                        JournalEntrySourceType.CashVoucher
                }
                into voucherRows
            from voucher in voucherRows.DefaultIfEmpty()
            join monetaryRevaluation in monetaryRevaluations
                on new
                {
                    line.JournalEntry.SourceId,
                    line.JournalEntry.SourceType
                }
                equals new
                {
                    SourceId = (int?)monetaryRevaluation.Id,
                    SourceType = (JournalEntrySourceType?)
                        JournalEntrySourceType.MonetaryAccountRevaluation
                }
                into monetaryRevaluationRows
            from monetaryRevaluation in monetaryRevaluationRows.DefaultIfEmpty()
            select new PartnerStatementRaw
            {
                JournalEntryLineId = line.Id,
                JournalEntryId = line.JournalEntryId,
                CashVoucherId = voucher != null && voucher.IsPosted &&
                    voucher.FiscalYearId == fiscalYearId && voucher.BusinessPartnerId == partnerId &&
                    voucher.InvoiceId == null && voucher.InvoicePayment == null &&
                    voucher.CashboxTransferId == null && voucher.Cashbox != null
                    ? voucher.Id
                    : null,
                CashboxId = voucher == null ? null : voucher.CashboxId,
                CashboxName = voucher == null || voucher.Cashbox == null ? null : voucher.Cashbox.Name,
                CashDirection = voucher == null ? null : voucher.Direction,
                IsOpening = line.JournalEntry.SourceType ==
                    JournalEntrySourceType.FiscalYearClosing,
                SourceId = line.Id,
                SourceType = line.JournalEntry.SourceType ==
                    JournalEntrySourceType.PartnerOpeningBalance
                        ? PartnerStatementSourceType.OpeningBalance
                        : line.JournalEntry.SourceType ==
                          JournalEntrySourceType.Invoice
                            ? PartnerStatementSourceType.Invoice
                            : line.JournalEntry.SourceType ==
                              JournalEntrySourceType.CashVoucher
                                ? PartnerStatementSourceType.CashVoucher
                                : PartnerStatementSourceType.JournalEntry,
                EntryType = line.JournalEntry.EntryType,
                Date = line.JournalEntry.EntryDate,
                CreatedOn = line.JournalEntry.PostedOn,
                DocumentNumber = invoice != null
                    ? invoice.InvoiceNumber
                    : voucher != null
                        ? voucher.VoucherNumber
                        : line.JournalEntry.SourceNumber ??
                          line.JournalEntry.EntryNumber,
                MovementType = invoice != null
                    ? invoice.InvoiceType == InvoiceType.Sales
                        ? BusinessPartnerMovementType.Sales
                        : invoice.InvoiceType == InvoiceType.Purchase
                            ? BusinessPartnerMovementType.Purchase
                            : invoice.InvoiceType == InvoiceType.SalesReturn
                                ? BusinessPartnerMovementType.SalesReturn
                                : BusinessPartnerMovementType.PurchaseReturn
                    : voucher != null
                        ? voucher.Direction == CashDirection.Receipt
                            ? BusinessPartnerMovementType.CashReceipt
                            : BusinessPartnerMovementType.CashPayment
                        : null,
                Description = line.Description ??
                    line.JournalEntry.Description,
                // Only native-currency lines represent partner units. Keep
                // base-currency revaluation and carry-forward adjustments in
                // the base amounts without adding them to foreign balances.
                Debit = line.Currency == partnerCurrency
                    ? line.TransactionDebit
                    : 0m,
                Credit = line.Currency == partnerCurrency
                    ? line.TransactionCredit
                    : 0m,
                ExchangeRate = monetaryRevaluation == null
                    ? line.ExchangeRate
                    : monetaryRevaluation.ClosingRate,
                BaseDebit = line.Debit,
                BaseCredit = line.Credit,
                ReferenceNumber = voucher != null
                    ? voucher.ReferenceNumber
                    : invoice != null
                        ? invoice.PartnerInvoiceNo
                        : null,
                CashMovementTypeId = voucher == null
                    ? null
                    : voucher.CashMovementTypeId,
                Classification = voucher == null
                    ? null
                    : voucher.Classification ??
                      (voucher.CashMovementType == null
                          ? null
                          : voucher.CashMovementType.Classification)
            };
    }

    private IQueryable<DriverStatementRaw> CreateDriverRows(
        int driverId,
        int fiscalYearId)
    {
        var vouchers = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher => voucher.CompanyId == companyId);
        var trips = dbContext.DriverTrips
            .AsNoTracking()
            .Where(trip => trip.CompanyId == companyId);

        return
            from line in PostedLedgerLines()
            where line.PartyType == JournalPartyType.Driver &&
                  line.PartyId == driverId &&
                  line.JournalEntry.FiscalYearId == fiscalYearId
            join voucher in vouchers
                on new
                {
                    line.JournalEntry.SourceId,
                    line.JournalEntry.SourceType
                }
                equals new
                {
                    SourceId = (int?)voucher.Id,
                    SourceType = (JournalEntrySourceType?)
                        JournalEntrySourceType.CashVoucher
                }
                into voucherRows
            from voucher in voucherRows.DefaultIfEmpty()
            join trip in trips
                on new
                {
                    line.JournalEntry.SourceId,
                    line.JournalEntry.SourceType
                }
                equals new
                {
                    SourceId = (int?)trip.Id,
                    SourceType = (JournalEntrySourceType?)
                        JournalEntrySourceType.DriverTrip
                }
                into tripRows
            from trip in tripRows.DefaultIfEmpty()
            select new DriverStatementRaw
            {
                JournalEntryLineId = line.Id,
                JournalEntryId = line.JournalEntryId,
                IsCarriedOpening = line.JournalEntry.SourceType ==
                    JournalEntrySourceType.FiscalYearClosing,
                IsManualOpening = line.JournalEntry.SourceType ==
                    JournalEntrySourceType.DriverOpeningBalance,
                SourceType = line.JournalEntry.SourceType ==
                    JournalEntrySourceType.CashVoucher
                        ? DriverStatementSourceType.CashVoucher
                        : line.JournalEntry.SourceType ==
                          JournalEntrySourceType.DriverTrip
                            ? DriverStatementSourceType.DriverTrip
                            : DriverStatementSourceType.JournalEntry,
                Date = line.JournalEntry.EntryDate,
                CreatedOn = line.JournalEntry.PostedOn,
                DocumentNumber = voucher != null
                    ? voucher.VoucherNumber
                    : trip != null
                        ? line.JournalEntry.SourceNumber ?? trip.InvoiceNumber ?? "TR-" + trip.Id
                        : line.JournalEntry.SourceNumber ??
                          line.JournalEntry.EntryNumber,
                InvoiceNumber = trip != null
                    ? trip.InvoiceNumber
                    : voucher != null && voucher.DriverTrip != null
                        ? voucher.DriverTrip.InvoiceNumber
                        : null,
                DriverTripId = trip != null
                    ? (int?)trip.Id
                    : voucher == null
                        ? null
                        : voucher.DriverTripId,
                BusinessPartnerId = trip != null
                    ? (int?)trip.BusinessPartnerId
                    : voucher != null && voucher.DriverTrip != null
                        ? voucher.DriverTrip.BusinessPartnerId
                        : null,
                BusinessPartnerName = trip != null
                    ? trip.BusinessPartner == null ? null : trip.BusinessPartner.Name
                    : voucher != null && voucher.DriverTrip != null && voucher.DriverTrip.BusinessPartner != null
                        ? voucher.DriverTrip.BusinessPartner.Name
                        : null,
                CountryName = trip != null
                    ? trip.Invoice == null || trip.Invoice.Country == null
                        ? null
                        : trip.Invoice.Country.Name
                    : voucher != null && voucher.DriverTrip != null &&
                      voucher.DriverTrip.Invoice != null &&
                      voucher.DriverTrip.Invoice.Country != null
                        ? voucher.DriverTrip.Invoice!.Country!.Name
                        : null,
                MovementTypeName = voucher != null &&
                    voucher.CashMovementType != null
                        ? voucher.CashMovementType.Name
                        : trip != null
                            ? "تكلفة رحلة"
                            : line.JournalEntry.EntryType ==
                              JournalEntryType.Adjustment
                                ? "قيد تسوية"
                                : line.JournalEntry.EntryType ==
                                  JournalEntryType.Manual
                                    ? "قيد يدوي"
                                    : "قيد محاسبي",
                Description = line.Description ??
                    line.JournalEntry.Description,
                Debit = line.Debit,
                Credit = line.Credit,
                CashPaid = voucher == null ? 0m : line.Debit,
                CashReceived = voucher == null ? 0m : line.Credit,
                TripCost = trip == null ? 0m : line.Credit - line.Debit,
                CashboxId = voucher == null ? null : (int?)voucher.CashboxId,
                CashboxName = voucher != null && voucher.Cashbox != null
                    ? voucher.Cashbox.Name
                    : null,
                ReferenceNumber = voucher == null
                    ? null
                    : voucher.ReferenceNumber,
                Direction = voucher == null
                    ? null
                    : (CashDirection?)voucher.Direction,
                CashMovementTypeId = voucher == null
                    ? null
                    : voucher.CashMovementTypeId,
                Classification = voucher == null
                    ? null
                    : voucher.Classification ??
                      (voucher.CashMovementType == null
                          ? null
                          : voucher.CashMovementType.Classification)
            };
    }

    private static Error? ValidatePagination(PaginationRequest pagination) =>
        pagination.PageNumber <= 0 ||
        pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize
            ? PaginationErrors.Invalid()
            : null;

    private static int GetOffset(PaginationRequest pagination, int totalCount)
    {
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        return offset >= totalCount ? totalCount : (int)offset;
    }

    private static int GetTotalPages(int totalCount, int pageSize) =>
        (int)Math.Ceiling(totalCount / (double)pageSize);

    private static string PartnerMovementName(
        BusinessPartnerMovementType? movementType,
        PartnerStatementSourceType sourceType,
        JournalEntryType entryType) => movementType switch
        {
            BusinessPartnerMovementType.Sales => "فاتورة بيع",
            BusinessPartnerMovementType.SalesReturn => "مرتجع بيع",
            BusinessPartnerMovementType.Purchase => "فاتورة شراء",
            BusinessPartnerMovementType.PurchaseReturn => "مرتجع شراء",
            BusinessPartnerMovementType.CashReceipt => "سند قبض",
            BusinessPartnerMovementType.CashPayment => "سند صرف",
            _ when sourceType == PartnerStatementSourceType.OpeningBalance =>
                "رصيد افتتاحي",
            _ when entryType == JournalEntryType.Adjustment => "قيد تسوية",
            _ when entryType == JournalEntryType.Manual => "قيد يدوي",
            _ when entryType == JournalEntryType.Opening => "قيد افتتاحي",
            _ => "قيد محاسبي"
        };

    private static string OpeningBalanceName(DateOnly fromDate, DateOnly fiscalYearStart, bool hasCarriedOpening) =>
        fromDate > fiscalYearStart
            ? "رصيد افتتاحي للفترة"
            : hasCarriedOpening ? "رصيد افتتاحي مرحل" : "رصيد افتتاحي";

    private static string DriverSourceName(DriverStatementSourceType sourceType) =>
        sourceType switch
        {
            DriverStatementSourceType.DriverTrip => "رحلة سائق",
            DriverStatementSourceType.CashVoucher => "سند نقدية",
            _ => "قيد محاسبي"
        };

    private static string PartnerBalanceDescription(decimal balance) =>
        balance switch
        {
            > 0m => "عليه",
            < 0m => "له",
            _ => "مسدد"
        };

    private static string DriverBalanceDescription(decimal balance) =>
        balance switch
        {
            > 0m => "مبلغ مطلوب من السائق",
            < 0m => "مبلغ مطلوب دفعه للسائق",
            _ => "لا يوجد مبلغ مستحق"
        };
}
