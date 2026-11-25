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
    public async Task<Result<CashboxStatementResponse>> GetCashboxStatementAsync(
        PaginationRequest pagination,
        CashboxStatementFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        if (cashboxAccessService is not null)
        {
            var access = await cashboxAccessService.EnsureViewAsync(filters.CashboxId, cancellationToken);
            if (access.IsFailure)
                return Result<CashboxStatementResponse>.Failure(access.Errors);
        }
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<CashboxStatementResponse>.Failure(paginationError);
        }

        var fiscalYear = await ResolveFiscalYearAsync(
            filters.FiscalYearId,
            filters.FromDate,
            filters.ToDate,
            cancellationToken);
        if (fiscalYear is null)
        {
            return Result<CashboxStatementResponse>.Failure(
                FiscalYearNotFound(filters.FiscalYearId));
        }
        var fromDate = filters.FromDate ?? fiscalYear.StartDate;
        var toDate = filters.ToDate ?? fiscalYear.EndDate;

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

        var allRows = CreateCashboxRows(
            cashbox.Id,
            cashbox.Currency,
            fiscalYear.Id);
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

        if (fromDate > fiscalYear.StartDate)
        {
            openingBalance += await nonOpeningRows
                .Where(row => row.Date < fromDate)
                .SumAsync(
                    row => (decimal?)(row.ReceiptAmount - row.PaymentAmount),
                    cancellationToken) ?? 0m;
            baseOpeningBalance += await nonOpeningRows
                .Where(row => row.Date < fromDate)
                .SumAsync(
                    row => (decimal?)(row.BaseReceiptAmount -
                        row.BasePaymentAmount),
                    cancellationToken) ?? 0m;
        }

        var search = filters.Search?.Trim();
        var voucherNumber = filters.VoucherNumber?.Trim();
        var query = nonOpeningRows
            .Where(row => row.Date >= fromDate)
            .Where(row => row.Date <= toDate)
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

        var movementCount = await query.CountAsync(cancellationToken);
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
        // The opening balance is a statement anchor, not a movement. Keep it
        // outside the movement query so filters/search cannot remove it, then
        // account for it as one logical row during pagination.
        var totalCount = movementCount + 1;
        var offset = GetOffset(pagination, totalCount);
        var openingRowIncluded = offset == 0;
        var movementOffset = openingRowIncluded ? 0 : offset - 1;
        var movementPageSize = openingRowIncluded
            ? Math.Max(pagination.PageSize - 1, 0)
            : pagination.PageSize;
        var precedingEffect = movementOffset == 0
            ? 0m
            : await ordered.Take(movementOffset).SumAsync(
                row => (decimal?)(row.ReceiptAmount - row.PaymentAmount),
                cancellationToken) ?? 0m;
        var precedingBaseEffect = movementOffset == 0
            ? 0m
            : await ordered.Take(movementOffset).SumAsync(
                row => (decimal?)(row.BaseReceiptAmount -
                    row.BasePaymentAmount),
                cancellationToken) ?? 0m;
        var pageRows = movementPageSize == 0 || movementOffset >= movementCount
            ? []
            : await ordered
                .Skip(movementOffset)
                .Take(movementPageSize)
                .ToListAsync(cancellationToken);

        var runningBalance = openingBalance + precedingEffect;
        var runningBaseBalance = baseOpeningBalance + precedingBaseEffect;
        var items = new List<CashboxStatementItemResponse>(pagination.PageSize);
        if (openingRowIncluded)
        {
            items.Add(new CashboxStatementItemResponse(
                CashVoucherId: null,
                Date: fromDate,
                VoucherNumber: "OPENING-BALANCE",
                MovementName: "رصيد افتتاحي",
                Description: fromDate > fiscalYear.StartDate
                    ? $"رصيد افتتاحي للفترة قبل {fromDate:yyyy-MM-dd}"
                    : "الرصيد الافتتاحي",
                PartyName: null,
                ReceiptAmount: 0m,
                PaymentAmount: 0m,
                Balance: openingBalance,
                ReferenceNumber: null)
            {
                Currency = cashbox.Currency,
                BaseCurrency = cashbox.BaseCurrency,
                ExchangeRate = cashbox.OpeningExchangeRate,
                IsBaseCurrency = cashbox.Currency == cashbox.BaseCurrency,
                BaseReceiptAmount = 0m,
                BasePaymentAmount = 0m,
                BaseBalance = baseOpeningBalance,
                IsOpeningBalance = true,
                IsReadOnly = true,
                JournalEntryId = null,
                JournalEntryLineId = null,
                SourceType = JournalEntrySourceType.CashboxOpeningBalance
            });
        }

        items.AddRange(pageRows.Select(row =>
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
        }));

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
        var accessScope = cashboxAccessService is null ? null
            : await cashboxAccessService.GetScopeAsync(cancellationToken);
        if (accessScope?.IsFailure == true) return Result<PartnerStatementResponse>.Failure(accessScope.Errors);
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<PartnerStatementResponse>.Failure(paginationError);
        }

        var fiscalYear = await ResolveFiscalYearAsync(
            filters.FiscalYearId,
            filters.FromDate,
            filters.ToDate,
            cancellationToken);
        if (fiscalYear is null)
        {
            return Result<PartnerStatementResponse>.Failure(
                FiscalYearNotFound(filters.FiscalYearId));
        }
        var fromDate = filters.FromDate ?? fiscalYear.StartDate;
        var toDate = filters.ToDate ?? fiscalYear.EndDate;

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

        var allRows = CreatePartnerRows(partner.Id, partner.Currency, fiscalYear.Id);
        var openingRows = allRows.Where(row => row.IsOpening);
        var hasCarriedOpening = await openingRows.AnyAsync(cancellationToken);
        var nonOpeningRows = allRows.Where(row => !row.IsOpening);
        var openingBalance = await openingRows
                .SumAsync(row => (decimal?)(row.Debit - row.Credit),
                    cancellationToken) ?? 0m;
        var baseOpeningBalance = await openingRows
                .SumAsync(row => (decimal?)(row.BaseDebit - row.BaseCredit),
                    cancellationToken) ?? 0m;
        if (fromDate > fiscalYear.StartDate)
        {
            openingBalance += await nonOpeningRows
                .Where(row => row.Date < fromDate)
                .SumAsync(row => (decimal?)(row.Debit - row.Credit),
                    cancellationToken) ?? 0m;
            baseOpeningBalance += await nonOpeningRows
                .Where(row => row.Date < fromDate)
                .SumAsync(row => (decimal?)(row.BaseDebit - row.BaseCredit),
                    cancellationToken) ?? 0m;
        }

        var search = filters.Search?.Trim();
        var query = nonOpeningRows
            .Where(row => row.Date >= fromDate)
            .Where(row => row.Date <= toDate)
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

        var movementCount = await query.CountAsync(cancellationToken);
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
        var totalCount = movementCount + 1;
        var offset = GetOffset(pagination, totalCount);
        var openingRowIncluded = offset == 0;
        var movementOffset = openingRowIncluded ? 0 : offset - 1;
        var movementPageSize = openingRowIncluded
            ? Math.Max(pagination.PageSize - 1, 0)
            : pagination.PageSize;
        var precedingEffect = movementOffset == 0
            ? 0m
            : await ordered.Take(movementOffset).SumAsync(
                row => (decimal?)(row.Debit - row.Credit), cancellationToken)
                ?? 0m;
        var precedingBaseEffect = movementOffset == 0
            ? 0m
            : await ordered.Take(movementOffset).SumAsync(
                row => (decimal?)(row.BaseDebit - row.BaseCredit),
                cancellationToken) ?? 0m;
        var pageRows = movementPageSize == 0 || movementOffset >= movementCount
            ? []
            : await ordered
                .Skip(movementOffset)
                .Take(movementPageSize)
                .ToListAsync(cancellationToken);

        var runningBalance = openingBalance + precedingEffect;
        var runningBaseBalance = baseOpeningBalance + precedingBaseEffect;
        var items = new List<PartnerStatementItemResponse>(pagination.PageSize);
        if (openingRowIncluded)
        {
            var openingName = OpeningBalanceName(fromDate, fiscalYear.StartDate, hasCarriedOpening);
            items.Add(new PartnerStatementItemResponse(
                Date: fromDate,
                DocumentNumber: "OPENING-BALANCE",
                MovementName: openingName,
                Description: openingName,
                DebitAmount: 0m,
                CreditAmount: 0m,
                BalanceAmount: Math.Abs(openingBalance),
                BalanceDescription: PartnerBalanceDescription(openingBalance),
                ReferenceNumber: null)
            {
                SourceType = PartnerStatementSourceType.OpeningBalance,
                ExchangeRate = partner.Currency == partner.BaseCurrency ? 1m : 0m,
                BaseBalanceAmount = Math.Abs(baseOpeningBalance),
                IsOpeningBalance = true,
                IsReadOnly = true
            });
        }
        items.AddRange(pageRows.Select(row =>
        {
            runningBalance += row.Debit - row.Credit;
            runningBaseBalance += row.BaseDebit - row.BaseCredit;
            var canViewVoucher = row.CashVoucherId.HasValue &&
                row.CashboxId is int cashboxId &&
                (accessScope is null || accessScope.Value.CanView(cashboxId));
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
                JournalEntryId = accessScope is null || accessScope.Value.HasAllCashboxes ? row.JournalEntryId : null,
                JournalEntryLineId = row.JournalEntryLineId,
                SourceType = row.SourceType,
                CashVoucherId = canViewVoucher ? row.CashVoucherId : null,
                CashboxId = canViewVoucher ? row.CashboxId : null,
                CashboxName = canViewVoucher ? row.CashboxName : null,
                CashDirection = canViewVoucher ? row.CashDirection : null,
                FiscalYearId = canViewVoucher ? fiscalYear.Id : null
            };
        }));

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
        var accessScope = cashboxAccessService is null ? null
            : await cashboxAccessService.GetScopeAsync(cancellationToken);
        if (accessScope?.IsFailure == true) return Result<DriverStatementResponse>.Failure(accessScope.Errors);
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<DriverStatementResponse>.Failure(paginationError);
        }

        var fiscalYear = await ResolveFiscalYearAsync(
            filters.FiscalYearId,
            filters.FromDate,
            filters.ToDate,
            cancellationToken);
        if (fiscalYear is null)
        {
            return Result<DriverStatementResponse>.Failure(
                FiscalYearNotFound(filters.FiscalYearId));
        }
        var fromDate = filters.FromDate ?? fiscalYear.StartDate;
        var toDate = filters.ToDate ?? fiscalYear.EndDate;

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

        var allRows = CreateDriverRows(driver.Id, fiscalYear.Id);
        var carriedOpening = await allRows.Where(row => row.IsCarriedOpening)
            .SumAsync(row => (decimal?)(row.Debit - row.Credit), cancellationToken);
        var manualOpening = await allRows.Where(row => row.IsManualOpening)
            .SumAsync(row => (decimal?)(row.Debit - row.Credit), cancellationToken);
        var hasCarriedOpening = carriedOpening.GetValueOrDefault() != 0m;
        var nonOpeningRows = allRows.Where(row => !row.IsCarriedOpening && !row.IsManualOpening);
        var openingBalance = carriedOpening.GetValueOrDefault() + manualOpening.GetValueOrDefault();
        if (fromDate > fiscalYear.StartDate)
        {
            openingBalance += await nonOpeningRows
                .Where(row => row.Date < fromDate)
                .SumAsync(row => (decimal?)(row.Debit - row.Credit),
                    cancellationToken) ?? 0m;
        }
        var search = filters.Search?.Trim();
        var invoiceNumber = filters.InvoiceNumber?.Trim();
        var query = nonOpeningRows
            .Where(row => row.Date >= fromDate)
            .Where(row => row.Date <= toDate)
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

        var movementCount = await query.CountAsync(cancellationToken);
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
        var totalCount = movementCount + 1;
        var offset = GetOffset(pagination, totalCount);
        var openingRowIncluded = offset == 0;
        var movementOffset = openingRowIncluded ? 0 : offset - 1;
        var movementPageSize = openingRowIncluded
            ? Math.Max(pagination.PageSize - 1, 0)
            : pagination.PageSize;
        var precedingEffect = movementOffset == 0
            ? 0m
            : await ordered.Take(movementOffset).SumAsync(
                row => (decimal?)(row.Debit - row.Credit), cancellationToken)
                ?? 0m;
        var pageRows = movementPageSize == 0 || movementOffset >= movementCount
            ? []
            : await ordered
                .Skip(movementOffset)
                .Take(movementPageSize)
                .ToListAsync(cancellationToken);

        var runningBalance = openingBalance + precedingEffect;
        var items = new List<DriverStatementItemResponse>(pagination.PageSize);
        if (openingRowIncluded)
        {
            var openingName = manualOpening.GetValueOrDefault() != 0m && !hasCarriedOpening && fromDate == fiscalYear.StartDate
                ? "رصيد افتتاحي مدخل"
                : OpeningBalanceName(fromDate, fiscalYear.StartDate, hasCarriedOpening);
            items.Add(new DriverStatementItemResponse(
                SourceId: 0,
                Date: fromDate,
                DocumentNumber: "OPENING-BALANCE",
                SourceName: openingName,
                InvoiceNumber: null,
                DriverTripId: null,
                DriverTripNumber: null,
                MovementName: openingName,
                Description: openingName,
                AmountPaidToDriver: 0m,
                AmountReceivedFromDriver: 0m,
                TripCost: 0m,
                BalanceAmount: Math.Abs(openingBalance),
                BalanceDescription: DriverBalanceDescription(openingBalance),
                CashboxName: null,
                ReferenceNumber: null)
            {
                SourceType = DriverStatementSourceType.OpeningBalance,
                IsOpeningBalance = true,
                IsReadOnly = true
            });
        }
        items.AddRange(pageRows.Select(row =>
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
                CashboxName: accessScope is null || accessScope.Value.HasAllCashboxes ||
                    (row.CashboxId.HasValue && accessScope.Value.CanView(row.CashboxId.Value)) ? row.CashboxName : null,
                ReferenceNumber: row.ReferenceNumber)
            {
                BusinessPartnerId = row.BusinessPartnerId,
                BusinessPartnerName = row.BusinessPartnerName,
                CountryName = row.CountryName,
                DebitAmount = row.Debit,
                CreditAmount = row.Credit,
                JournalEntryId = accessScope is null || accessScope.Value.HasAllCashboxes ? row.JournalEntryId : null,
                JournalEntryLineId = row.JournalEntryLineId,
                SourceType = row.SourceType
            };
        }));

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
}
