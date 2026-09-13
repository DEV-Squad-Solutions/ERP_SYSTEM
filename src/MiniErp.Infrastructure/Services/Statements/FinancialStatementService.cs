using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.Statements.StatementErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Statements;

public sealed partial class FinancialStatementService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IFinancialStatementService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<CashboxStatementResponse>> GetCashboxStatementAsync(
        PaginationRequest pagination,
        CashboxStatementFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<CashboxStatementResponse>.Failure(paginationError);
        }

        var cashbox = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == filters.CashboxId)
            .Select(entity => new
            {
                entity.Id,
                entity.Name,
                entity.Currency,
                entity.OpeningBalanceDate,
                entity.OpeningExchangeRate,
                BaseCurrency = entity.Company.Settings == null
                    ? CurrencyCode.EGP
                    : entity.Company.Settings.BaseCurrency
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (cashbox is null)
        {
            return Result<CashboxStatementResponse>.Failure(
                CashboxNotFound(filters.CashboxId));
        }

        var allRows = CreateCashboxRows(cashbox.Id);
        var openingJournal = await allRows
            .Where(row => row.IsOpening)
            .GroupBy(_ => 1)
            .Select(rows => new
            {
                Amount = rows.Sum(row => row.ReceiptAmount - row.PaymentAmount),
                BaseAmount = rows.Sum(row =>
                    row.BaseReceiptAmount - row.BasePaymentAmount)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var openingBalance = openingJournal?.Amount ?? 0m;
        var baseOpeningBalance = openingJournal?.BaseAmount ?? 0m;
        var nonOpeningRows = allRows.Where(row => !row.IsOpening);

        if (filters.FromDate.HasValue)
        {
            openingBalance += await nonOpeningRows
                .Where(row => row.Date < filters.FromDate.Value)
                .SumAsync(
                    row => (decimal?)(row.ReceiptAmount - row.PaymentAmount),
                    cancellationToken) ?? 0m;
            baseOpeningBalance += await nonOpeningRows
                .Where(row => row.Date < filters.FromDate.Value)
                .SumAsync(
                    row => (decimal?)(row.BaseReceiptAmount -
                        row.BasePaymentAmount),
                    cancellationToken) ?? 0m;
        }

        var search = filters.Search?.Trim();
        var voucherNumber = filters.VoucherNumber?.Trim();
        var query = nonOpeningRows
            .Where(row =>
                !filters.FromDate.HasValue ||
                row.Date >= filters.FromDate.Value)
            .Where(row =>
                !filters.ToDate.HasValue ||
                row.Date <= filters.ToDate.Value)
            .Where(row =>
                !filters.Direction.HasValue ||
                (filters.Direction == CashDirection.Receipt
                    ? row.ReceiptAmount > 0m
                    : row.PaymentAmount > 0m))
            .Where(row =>
                !filters.CashMovementTypeId.HasValue ||
                row.CashMovementTypeId == filters.CashMovementTypeId.Value)
            .Where(row =>
                !filters.Classification.HasValue ||
                row.Classification == filters.Classification.Value)
            .Where(row =>
                !filters.PartyType.HasValue ||
                row.CashPartyType == filters.PartyType.Value)
            .Where(row =>
                !filters.BusinessPartnerId.HasValue ||
                row.BusinessPartnerId == filters.BusinessPartnerId.Value)
            .Where(row =>
                !filters.DriverId.HasValue ||
                row.DriverId == filters.DriverId.Value)
            .Where(row =>
                !filters.DriverTripId.HasValue ||
                row.DriverTripId == filters.DriverTripId.Value)
            .Where(row =>
                !filters.EmployeeId.HasValue ||
                row.EmployeeId == filters.EmployeeId.Value)
            .Where(row =>
                string.IsNullOrEmpty(voucherNumber) ||
                row.DocumentNumber.Contains(voucherNumber))
            .Where(row =>
                string.IsNullOrEmpty(search) ||
                row.DocumentNumber.Contains(search) ||
                (row.MovementName != null &&
                 row.MovementName.Contains(search)) ||
                (row.PartyName != null && row.PartyName.Contains(search)) ||
                (row.ExternalPartyName != null &&
                 row.ExternalPartyName.Contains(search)) ||
                (row.ReferenceNumber != null &&
                 row.ReferenceNumber.Contains(search)) ||
                (row.Description != null && row.Description.Contains(search)));

        var totalCount = await query.CountAsync(cancellationToken);
        var totals = await query
            .GroupBy(_ => 1)
            .Select(rows => new
            {
                Receipts = rows.Sum(row => row.ReceiptAmount),
                Payments = rows.Sum(row => row.PaymentAmount),
                BaseReceipts = rows.Sum(row => row.BaseReceiptAmount),
                BasePayments = rows.Sum(row => row.BasePaymentAmount)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var totalReceipts = totals?.Receipts ?? 0m;
        var totalPayments = totals?.Payments ?? 0m;
        var totalBaseReceipts = totals?.BaseReceipts ?? 0m;
        var totalBasePayments = totals?.BasePayments ?? 0m;

        var ordered = query
            .OrderBy(row => row.Date)
            .ThenBy(row => row.CreatedOn)
            .ThenBy(row => row.DocumentNumber)
            .ThenBy(row => row.JournalEntryLineId);
        var offset = GetOffset(pagination, totalCount);
        var precedingEffect = offset == 0
            ? 0m
            : await ordered.Take(offset).SumAsync(
                row => (decimal?)(row.ReceiptAmount - row.PaymentAmount),
                cancellationToken) ?? 0m;
        var precedingBaseEffect = offset == 0
            ? 0m
            : await ordered.Take(offset).SumAsync(
                row => (decimal?)(row.BaseReceiptAmount -
                    row.BasePaymentAmount),
                cancellationToken) ?? 0m;
        var pageRows = offset >= totalCount
            ? []
            : await ordered
                .Skip(offset)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

        var runningBalance = openingBalance + precedingEffect;
        var runningBaseBalance = baseOpeningBalance + precedingBaseEffect;
        var items = pageRows.Select(row =>
        {
            runningBalance += row.ReceiptAmount - row.PaymentAmount;
            runningBaseBalance +=
                row.BaseReceiptAmount - row.BasePaymentAmount;
            return new CashboxStatementItemResponse(
                CashVoucherId: row.CashVoucherId,
                Date: row.Date,
                VoucherNumber: row.DocumentNumber,
                MovementName: row.MovementName ?? "حركة قيد",
                Description: row.Description,
                PartyName: row.PartyName ?? row.ExternalPartyName,
                ReceiptAmount: row.ReceiptAmount,
                PaymentAmount: row.PaymentAmount,
                Balance: runningBalance,
                ReferenceNumber: row.ReferenceNumber)
            {
                Currency = row.Currency,
                BaseCurrency = cashbox.BaseCurrency,
                ExchangeRate = row.ExchangeRate,
                IsBaseCurrency = row.Currency == cashbox.BaseCurrency,
                BaseReceiptAmount = row.BaseReceiptAmount,
                BasePaymentAmount = row.BasePaymentAmount,
                BaseBalance = runningBaseBalance,
                JournalEntryId = row.JournalEntryId,
                JournalEntryLineId = row.JournalEntryLineId,
                SourceType = row.SourceType
            };
        }).ToArray();

        return Result<CashboxStatementResponse>.Success(
            new CashboxStatementResponse(
                CashboxId: cashbox.Id,
                CashboxName: cashbox.Name,
                Currency: cashbox.Currency,
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: GetTotalPages(totalCount, pagination.PageSize),
                Summary: new CashboxStatementSummaryResponse(
                    OpeningBalance: openingBalance,
                    TotalReceipts: totalReceipts,
                    TotalPayments: totalPayments,
                    ClosingBalance: openingBalance + totalReceipts - totalPayments)
                {
                    BaseOpeningBalance = baseOpeningBalance,
                    BaseTotalReceipts = totalBaseReceipts,
                    BaseTotalPayments = totalBasePayments,
                    BaseClosingBalance = baseOpeningBalance +
                        totalBaseReceipts - totalBasePayments
                })
            {
                BaseCurrency = cashbox.BaseCurrency,
                OpeningBalanceDate = cashbox.OpeningBalanceDate,
                OpeningExchangeRate = cashbox.OpeningExchangeRate,
                IsBaseCurrency = cashbox.Currency == cashbox.BaseCurrency
            });
    }

    public async Task<Result<PartnerStatementResponse>> GetPartnerStatementAsync(
        PaginationRequest pagination,
        PartnerStatementFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<PartnerStatementResponse>.Failure(paginationError);
        }

        var partner = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == filters.BusinessPartnerId)
            .Select(entity => new
            {
                entity.Id,
                entity.Name,
                entity.Currency,
                BaseCurrency = entity.Company.Settings == null
                    ? CurrencyCode.EGP
                    : entity.Company.Settings.BaseCurrency
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (partner is null)
        {
            return Result<PartnerStatementResponse>.Failure(
                PartnerNotFound(filters.BusinessPartnerId));
        }

        var allRows = CreatePartnerRows(partner.Id);
        var openingBalance = filters.FromDate.HasValue
            ? await allRows
                .Where(row => row.Date < filters.FromDate.Value)
                .SumAsync(row => (decimal?)(row.Debit - row.Credit),
                    cancellationToken) ?? 0m
            : 0m;
        var baseOpeningBalance = filters.FromDate.HasValue
            ? await allRows
                .Where(row => row.Date < filters.FromDate.Value)
                .SumAsync(row => (decimal?)(row.BaseDebit - row.BaseCredit),
                    cancellationToken) ?? 0m
            : 0m;

        var search = filters.Search?.Trim();
        var query = allRows
            .Where(row => !filters.FromDate.HasValue ||
                row.Date >= filters.FromDate.Value)
            .Where(row => !filters.ToDate.HasValue ||
                row.Date <= filters.ToDate.Value)
            .Where(row => !filters.SourceType.HasValue ||
                row.SourceType == filters.SourceType.Value)
            .Where(row => !filters.MovementType.HasValue ||
                row.MovementType == filters.MovementType.Value)
            .Where(row => !filters.CashMovementTypeId.HasValue ||
                row.CashMovementTypeId == filters.CashMovementTypeId.Value)
            .Where(row => !filters.Classification.HasValue ||
                row.Classification == filters.Classification.Value)
            .Where(row => string.IsNullOrEmpty(search) ||
                row.DocumentNumber.Contains(search) ||
                (row.Description != null && row.Description.Contains(search)) ||
                (row.ReferenceNumber != null &&
                 row.ReferenceNumber.Contains(search)));

        var totalCount = await query.CountAsync(cancellationToken);
        var totals = await query
            .GroupBy(_ => 1)
            .Select(rows => new
            {
                Debit = rows.Sum(row => row.Debit),
                Credit = rows.Sum(row => row.Credit),
                BaseDebit = rows.Sum(row => row.BaseDebit),
                BaseCredit = rows.Sum(row => row.BaseCredit)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var totalDebit = totals?.Debit ?? 0m;
        var totalCredit = totals?.Credit ?? 0m;
        var totalBaseDebit = totals?.BaseDebit ?? 0m;
        var totalBaseCredit = totals?.BaseCredit ?? 0m;
        var ordered = query
            .OrderBy(row => row.Date)
            .ThenBy(row => row.CreatedOn)
            .ThenBy(row => row.DocumentNumber)
            .ThenBy(row => row.JournalEntryLineId);
        var offset = GetOffset(pagination, totalCount);
        var precedingEffect = offset == 0
            ? 0m
            : await ordered.Take(offset).SumAsync(
                row => (decimal?)(row.Debit - row.Credit), cancellationToken)
                ?? 0m;
        var precedingBaseEffect = offset == 0
            ? 0m
            : await ordered.Take(offset).SumAsync(
                row => (decimal?)(row.BaseDebit - row.BaseCredit),
                cancellationToken) ?? 0m;
        var pageRows = offset >= totalCount
            ? []
            : await ordered
                .Skip(offset)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

        var runningBalance = openingBalance + precedingEffect;
        var runningBaseBalance = baseOpeningBalance + precedingBaseEffect;
        var items = pageRows.Select(row =>
        {
            runningBalance += row.Debit - row.Credit;
            runningBaseBalance += row.BaseDebit - row.BaseCredit;
            return new PartnerStatementItemResponse(
                Date: row.Date,
                DocumentNumber: row.DocumentNumber,
                MovementName: PartnerMovementName(
                    row.MovementType,
                    row.SourceType,
                    row.EntryType),
                Description: row.Description,
                DebitAmount: row.Debit,
                CreditAmount: row.Credit,
                BalanceAmount: Math.Abs(runningBalance),
                BalanceDescription: PartnerBalanceDescription(runningBalance),
                ReferenceNumber: row.ReferenceNumber)
            {
                ExchangeRate = row.ExchangeRate,
                BaseDebitAmount = row.BaseDebit,
                BaseCreditAmount = row.BaseCredit,
                BaseBalanceAmount = Math.Abs(runningBaseBalance),
                JournalEntryId = row.JournalEntryId,
                JournalEntryLineId = row.JournalEntryLineId,
                SourceType = row.SourceType
            };
        }).ToArray();

        var closingBalance = openingBalance + totalDebit - totalCredit;
        var baseClosingBalance =
            baseOpeningBalance + totalBaseDebit - totalBaseCredit;
        return Result<PartnerStatementResponse>.Success(
            new PartnerStatementResponse(
                BusinessPartnerId: partner.Id,
                BusinessPartnerName: partner.Name,
                Currency: partner.Currency,
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: GetTotalPages(totalCount, pagination.PageSize),
                Summary: new PartnerStatementSummaryResponse(
                    OpeningBalanceAmount: Math.Abs(openingBalance),
                    OpeningBalanceDescription:
                        PartnerBalanceDescription(openingBalance),
                    ClosingBalanceAmount: Math.Abs(closingBalance),
                    ClosingBalanceDescription:
                        PartnerBalanceDescription(closingBalance))
                {
                    BaseOpeningBalanceAmount = Math.Abs(baseOpeningBalance),
                    BaseClosingBalanceAmount = Math.Abs(baseClosingBalance)
                })
            {
                BaseCurrency = partner.BaseCurrency
            });
    }

    public async Task<Result<DriverStatementResponse>> GetDriverStatementAsync(
        PaginationRequest pagination,
        DriverStatementFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<DriverStatementResponse>.Failure(paginationError);
        }

        var driver = await dbContext.Drivers
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == filters.DriverId)
            .Select(entity => new
            {
                entity.Id,
                entity.Name,
                BaseCurrency = entity.Company.Settings == null
                    ? CurrencyCode.EGP
                    : entity.Company.Settings.BaseCurrency
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (driver is null)
        {
            return Result<DriverStatementResponse>.Failure(
                DriverNotFound(filters.DriverId));
        }

        var allRows = CreateDriverRows(driver.Id);
        var openingBalance = filters.FromDate.HasValue
            ? await allRows
                .Where(row => row.Date < filters.FromDate.Value)
                .SumAsync(row => (decimal?)(row.Debit - row.Credit),
                    cancellationToken) ?? 0m
            : 0m;
        var search = filters.Search?.Trim();
        var invoiceNumber = filters.InvoiceNumber?.Trim();
        var query = allRows
            .Where(row => !filters.FromDate.HasValue ||
                row.Date >= filters.FromDate.Value)
            .Where(row => !filters.ToDate.HasValue ||
                row.Date <= filters.ToDate.Value)
            .Where(row => !filters.Direction.HasValue ||
                row.Direction == filters.Direction.Value)
            .Where(row => !filters.CashMovementTypeId.HasValue ||
                row.CashMovementTypeId == filters.CashMovementTypeId.Value)
            .Where(row => !filters.Classification.HasValue ||
                row.Classification == filters.Classification.Value)
            .Where(row => !filters.DriverTripId.HasValue ||
                row.DriverTripId == filters.DriverTripId.Value)
            .Where(row => string.IsNullOrEmpty(invoiceNumber) ||
                (row.InvoiceNumber != null &&
                 row.InvoiceNumber.Contains(invoiceNumber)))
            .Where(row => !filters.TransactionsWithoutTrip.HasValue ||
                (filters.TransactionsWithoutTrip.Value
                    ? row.SourceType == DriverStatementSourceType.CashVoucher &&
                      row.DriverTripId == null
                    : row.DriverTripId != null))
            .Where(row => !filters.HasCost.HasValue ||
                (row.SourceType == DriverStatementSourceType.DriverTrip &&
                 (row.TripCost > 0m) == filters.HasCost.Value))
            .Where(row => string.IsNullOrEmpty(search) ||
                row.DocumentNumber.Contains(search) ||
                (row.InvoiceNumber != null &&
                 row.InvoiceNumber.Contains(search)) ||
                (row.MovementTypeName != null &&
                 row.MovementTypeName.Contains(search)) ||
                (row.Description != null && row.Description.Contains(search)) ||
                (row.ReferenceNumber != null &&
                 row.ReferenceNumber.Contains(search)));

        var totalCount = await query.CountAsync(cancellationToken);
        var totals = await query
            .GroupBy(_ => 1)
            .Select(rows => new
            {
                Debit = rows.Sum(row => row.Debit),
                Credit = rows.Sum(row => row.Credit),
                CashPaid = rows.Sum(row => row.CashPaid),
                CashReceived = rows.Sum(row => row.CashReceived),
                TripCost = rows.Sum(row => row.TripCost)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var totalDebit = totals?.Debit ?? 0m;
        var totalCredit = totals?.Credit ?? 0m;
        var totalCashPaid = totals?.CashPaid ?? 0m;
        var totalCashReceived = totals?.CashReceived ?? 0m;
        var totalTripCost = totals?.TripCost ?? 0m;
        var ordered = query
            .OrderBy(row => row.Date)
            .ThenBy(row => row.CreatedOn)
            .ThenBy(row => row.DocumentNumber)
            .ThenBy(row => row.JournalEntryLineId);
        var offset = GetOffset(pagination, totalCount);
        var precedingEffect = offset == 0
            ? 0m
            : await ordered.Take(offset).SumAsync(
                row => (decimal?)(row.Debit - row.Credit), cancellationToken)
                ?? 0m;
        var pageRows = offset >= totalCount
            ? []
            : await ordered
                .Skip(offset)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

        var runningBalance = openingBalance + precedingEffect;
        var items = pageRows.Select(row =>
        {
            runningBalance += row.Debit - row.Credit;
            return new DriverStatementItemResponse(
                SourceId: row.JournalEntryLineId,
                Date: row.Date,
                DocumentNumber: row.DocumentNumber,
                SourceName: DriverSourceName(row.SourceType),
                InvoiceNumber: row.InvoiceNumber,
                DriverTripId: row.DriverTripId,
                DriverTripNumber: row.DriverTripId.HasValue
                    ? $"TR-{row.DriverTripId.Value}"
                    : null,
                MovementName: row.MovementTypeName ?? "قيد محاسبي",
                Description: row.Description,
                AmountPaidToDriver: row.CashPaid,
                AmountReceivedFromDriver: row.CashReceived,
                TripCost: row.TripCost,
                BalanceAmount: Math.Abs(runningBalance),
                BalanceDescription: DriverBalanceDescription(runningBalance),
                CashboxName: row.CashboxName,
                ReferenceNumber: row.ReferenceNumber)
            {
                BusinessPartnerId = row.BusinessPartnerId,
                BusinessPartnerName = row.BusinessPartnerName,
                CountryName = row.CountryName,
                DebitAmount = row.Debit,
                CreditAmount = row.Credit,
                JournalEntryId = row.JournalEntryId,
                JournalEntryLineId = row.JournalEntryLineId,
                SourceType = row.SourceType
            };
        }).ToArray();

        var closingBalance = openingBalance + totalDebit - totalCredit;
        return Result<DriverStatementResponse>.Success(
            new DriverStatementResponse(
                DriverId: driver.Id,
                DriverName: driver.Name,
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: GetTotalPages(totalCount, pagination.PageSize),
                Summary: new DriverStatementSummaryResponse(
                    OpeningBalanceAmount: Math.Abs(openingBalance),
                    OpeningBalanceDescription:
                        DriverBalanceDescription(openingBalance),
                    TotalPaidToDriver: totalCashPaid,
                    TotalReceivedFromDriver: totalCashReceived,
                    TotalTripCost: totalTripCost,
                    ClosingBalanceAmount: Math.Abs(closingBalance),
                    ClosingBalanceDescription:
                        DriverBalanceDescription(closingBalance))
                {
                    TotalDebits = totalDebit,
                    TotalCredits = totalCredit
                })
            {
                BaseCurrency = driver.BaseCurrency
            });
    }

    private IQueryable<CashboxStatementRaw> CreateCashboxRows(int cashboxId)
    {
        var vouchers = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher => voucher.CompanyId == companyId);

        return
            from line in PostedLedgerLines()
            where line.PartyType == JournalPartyType.Cashbox &&
                  line.PartyId == cashboxId
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
            select new CashboxStatementRaw
            {
                JournalEntryLineId = line.Id,
                JournalEntryId = line.JournalEntryId,
                CashVoucherId = voucher == null ? null : (int?)voucher.Id,
                SourceType = line.JournalEntry.SourceType,
                IsOpening = line.JournalEntry.SourceType ==
                    JournalEntrySourceType.CashboxOpeningBalance,
                Date = line.JournalEntry.EntryDate,
                CreatedOn = line.JournalEntry.PostedOn,
                DocumentNumber = voucher != null
                    ? voucher.VoucherNumber
                    : line.JournalEntry.SourceNumber ??
                      line.JournalEntry.EntryNumber,
                MovementName = voucher != null
                    ? voucher.CashMovementType != null
                        ? voucher.CashMovementType.Name
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
                        ? voucher.BusinessPartner.Name
                        : voucher != null && voucher.Driver != null
                            ? voucher.Driver.Name
                            : voucher != null && voucher.Employee != null
                                ? voucher.Employee.Name
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
                          : voucher.CashMovementType.Classification),
                Currency = line.Currency,
                ExchangeRate = line.ExchangeRate,
                ReceiptAmount = line.TransactionDebit,
                PaymentAmount = line.TransactionCredit,
                BaseReceiptAmount = line.Debit,
                BasePaymentAmount = line.Credit,
                ReferenceNumber = voucher == null
                    ? null
                    : voucher.ReferenceNumber
            };
    }

    private IQueryable<PartnerStatementRaw> CreatePartnerRows(int partnerId)
    {
        var invoices = dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.CompanyId == companyId);
        var vouchers = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher => voucher.CompanyId == companyId);

        return
            from line in PostedLedgerLines()
            where (line.PartyType == JournalPartyType.Customer ||
                   line.PartyType == JournalPartyType.Supplier) &&
                  line.PartyId == partnerId
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
            select new PartnerStatementRaw
            {
                JournalEntryLineId = line.Id,
                JournalEntryId = line.JournalEntryId,
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
                Debit = line.TransactionDebit,
                Credit = line.TransactionCredit,
                ExchangeRate = line.ExchangeRate,
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

    private IQueryable<DriverStatementRaw> CreateDriverRows(int driverId)
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
                  line.PartyId == driverId
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
                        ? line.JournalEntry.SourceNumber ?? trip.InvoiceNumber
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
                    ? trip.BusinessPartner.Name
                    : voucher != null && voucher.DriverTrip != null
                        ? voucher.DriverTrip.BusinessPartner.Name
                        : null,
                CountryName = trip != null
                    ? trip.Invoice.Country == null
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

    private sealed class CashboxStatementRaw
    {
        public int JournalEntryLineId { get; init; }
        public int JournalEntryId { get; init; }
        public int? CashVoucherId { get; init; }
        public JournalEntrySourceType? SourceType { get; init; }
        public bool IsOpening { get; init; }
        public DateOnly Date { get; init; }
        public DateTime CreatedOn { get; init; }
        public string DocumentNumber { get; init; } = string.Empty;
        public string? MovementName { get; init; }
        public string? Description { get; init; }
        public string? PartyName { get; init; }
        public string? ExternalPartyName { get; init; }
        public CashPartyType? CashPartyType { get; init; }
        public int? BusinessPartnerId { get; init; }
        public int? DriverId { get; init; }
        public int? DriverTripId { get; init; }
        public int? EmployeeId { get; init; }
        public int? CashMovementTypeId { get; init; }
        public CashMovementClassification? Classification { get; init; }
        public CurrencyCode Currency { get; init; }
        public decimal ExchangeRate { get; init; }
        public decimal ReceiptAmount { get; init; }
        public decimal PaymentAmount { get; init; }
        public decimal BaseReceiptAmount { get; init; }
        public decimal BasePaymentAmount { get; init; }
        public string? ReferenceNumber { get; init; }
    }

    private sealed class PartnerStatementRaw
    {
        public int JournalEntryLineId { get; init; }
        public int JournalEntryId { get; init; }
        public int SourceId { get; init; }
        public PartnerStatementSourceType SourceType { get; init; }
        public JournalEntryType EntryType { get; init; }
        public DateOnly Date { get; init; }
        public DateTime CreatedOn { get; init; }
        public string DocumentNumber { get; init; } = string.Empty;
        public BusinessPartnerMovementType? MovementType { get; init; }
        public string? Description { get; init; }
        public decimal Debit { get; init; }
        public decimal Credit { get; init; }
        public decimal ExchangeRate { get; init; }
        public decimal BaseDebit { get; init; }
        public decimal BaseCredit { get; init; }
        public string? ReferenceNumber { get; init; }
        public int? CashMovementTypeId { get; init; }
        public CashMovementClassification? Classification { get; init; }
    }

    private sealed class DriverStatementRaw
    {
        public int JournalEntryLineId { get; init; }
        public int JournalEntryId { get; init; }
        public DriverStatementSourceType SourceType { get; init; }
        public DateOnly Date { get; init; }
        public DateTime CreatedOn { get; init; }
        public string DocumentNumber { get; init; } = string.Empty;
        public string? InvoiceNumber { get; init; }
        public int? DriverTripId { get; init; }
        public int? BusinessPartnerId { get; init; }
        public string? BusinessPartnerName { get; init; }
        public string? CountryName { get; init; }
        public string? MovementTypeName { get; init; }
        public string? Description { get; init; }
        public decimal Debit { get; init; }
        public decimal Credit { get; init; }
        public decimal CashPaid { get; init; }
        public decimal CashReceived { get; init; }
        public decimal TripCost { get; init; }
        public string? CashboxName { get; init; }
        public string? ReferenceNumber { get; init; }
        public CashDirection? Direction { get; init; }
        public int? CashMovementTypeId { get; init; }
        public CashMovementClassification? Classification { get; init; }
    }
}
