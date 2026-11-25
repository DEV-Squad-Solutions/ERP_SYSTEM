using System.Data;
using static MiniErp.Application.Features.CashVouchers.CashVoucherErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Cashboxes;

namespace MiniErp.Infrastructure.Services.CashVouchers;

public sealed partial class CashVoucherService
{
    public async Task<Result<PagedResponse<CashVoucherResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CashVoucherFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        _ = paginationService;

        if (pagination.PageNumber <= 0 ||
            pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
        {
            return Result<PagedResponse<CashVoucherResponse>>.Failure(
                PaginationErrors.Invalid());
        }

        filters ??= new CashVoucherFilterRequest();
        var access = await CashboxPermissionGuard.ScopeAsync(cashboxAccessService, cancellationToken);
        if (access.IsFailure) return Result<PagedResponse<CashVoucherResponse>>.Failure(access.Errors);
        var scope = access.Value;
        if (filters.CashboxId is int selectedCashboxId)
        {
            var permission = await CashboxPermissionGuard.ViewAsync(cashboxAccessService, selectedCashboxId, cancellationToken);
            if (permission.IsFailure) return Result<PagedResponse<CashVoucherResponse>>.Failure(permission.Errors);
        }
        var fiscalYearResult = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: filters.FiscalYearId,
            fromDate: filters.FromDate,
            toDate: filters.ToDate,
            cancellationToken: cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result<PagedResponse<CashVoucherResponse>>.Failure(
                fiscalYearResult.Errors);
        }

        var fiscalYear = fiscalYearResult.Value;
        var search = filters.Search?.Trim();
        var voucherNumber = filters.VoucherNumber?.Trim();
        var accountIds = new HashSet<int>();

        if (filters.AccountId is int accountId &&
            filters.IncludeSubAccounts)
        {
            var expenseAccounts = await dbContext.Accounts
                .AsNoTracking()
                .Where(account =>
                    account.CompanyId == companyId &&
                    account.AccountType == AccountType.Expense)
                .Select(account => new
                {
                    account.Id,
                    account.ParentAccountId
                })
                .ToListAsync(cancellationToken);

            var childrenByParent = expenseAccounts
                .Where(account => account.ParentAccountId.HasValue)
                .GroupBy(account => account.ParentAccountId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(account => account.Id).ToList());

            if (expenseAccounts.Any(account => account.Id == accountId))
            {
                var pending = new Queue<int>();
                pending.Enqueue(accountId);

                while (pending.TryDequeue(out var currentAccountId))
                {
                    if (!accountIds.Add(currentAccountId) ||
                        !childrenByParent.TryGetValue(
                            currentAccountId,
                            out var childAccountIds))
                    {
                        continue;
                    }

                    foreach (var childAccountId in childAccountIds)
                    {
                        pending.Enqueue(childAccountId);
                    }
                }
            }
        }

        var query = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher => scope.HasAllCashboxes ||
                (voucher.CashboxId.HasValue && scope.ViewCashboxIds.Contains(voucher.CashboxId.Value) &&
                 (!voucher.CashboxTransferId.HasValue ||
                  (scope.ViewCashboxIds.Contains(voucher.CashboxTransfer!.SourceCashboxId) &&
                   scope.ViewCashboxIds.Contains(voucher.CashboxTransfer.DestinationCashboxId)))))
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.FiscalYearId == fiscalYear.FiscalYearId)
            .Where(voucher =>
                string.IsNullOrEmpty(search) ||
                voucher.VoucherNumber.Contains(search) ||
                (voucher.Cashbox != null &&
                 (voucher.Cashbox.Code.Contains(search) ||
                  voucher.Cashbox.Name.Contains(search))) ||
                (voucher.CashMovementType != null &&
                 voucher.CashMovementType.Name.Contains(search)) ||
                (voucher.BusinessPartner != null &&
                 (voucher.BusinessPartner.Code.Contains(search) ||
                  voucher.BusinessPartner.Name.Contains(search))) ||
                (voucher.Employee != null &&
                 (voucher.Employee.Code.Contains(search) ||
                  voucher.Employee.Name.Contains(search))) ||
                (voucher.Driver != null &&
                 (voucher.Driver.Code.Contains(search) ||
                  voucher.Driver.Name.Contains(search))) ||
                (voucher.Account != null &&
                 (voucher.Account.Code.Contains(search) ||
                  voucher.Account.Name.Contains(search))) ||
                (voucher.DriverTrip != null && voucher.DriverTrip.InvoiceNumber != null &&
                 voucher.DriverTrip.InvoiceNumber.Contains(search)) ||
                (voucher.Invoice != null &&
                 (voucher.Invoice.InvoiceNumber.Contains(search) ||
                  (voucher.Invoice.PartnerInvoiceNo != null &&
                   voucher.Invoice.PartnerInvoiceNo.Contains(search)))) ||
                (voucher.ExternalPartyName != null &&
                 voucher.ExternalPartyName.Contains(search)) ||
                (voucher.ReferenceNumber != null &&
                 voucher.ReferenceNumber.Contains(search)) ||
                (voucher.Description != null &&
                 voucher.Description.Contains(search)))
            .Where(voucher =>
                string.IsNullOrEmpty(voucherNumber) ||
                voucher.VoucherNumber.Contains(voucherNumber))
            .Where(voucher =>
                !filters.Direction.HasValue ||
                voucher.Direction == filters.Direction.Value)
            .Where(voucher =>
                !filters.CashboxId.HasValue ||
                voucher.CashboxId == filters.CashboxId.Value)
            .Where(voucher =>
                !filters.CashMovementTypeId.HasValue ||
                voucher.CashMovementTypeId ==
                filters.CashMovementTypeId.Value)
            .Where(voucher =>
                !filters.Classification.HasValue ||
                voucher.Classification == filters.Classification.Value ||
                (voucher.CashMovementType != null &&
                 voucher.CashMovementType.Classification ==
                 filters.Classification.Value) ||
                (filters.Classification.Value ==
                     CashMovementClassification.Expense &&
                 voucher.Account != null &&
                 voucher.Account.AccountType == AccountType.Expense) ||
                (filters.Classification.Value ==
                     CashMovementClassification.Revenue &&
                 voucher.Account != null &&
                 voucher.Account.AccountType == AccountType.Revenue))
            .Where(voucher =>
                !filters.PartyType.HasValue ||
                voucher.PartyType == filters.PartyType.Value)
            .Where(voucher =>
                !filters.BusinessPartnerId.HasValue ||
                voucher.BusinessPartnerId ==
                filters.BusinessPartnerId.Value)
            .Where(voucher =>
                !filters.DriverId.HasValue ||
                voucher.DriverId == filters.DriverId.Value)
            .Where(voucher =>
                !filters.DriverTripId.HasValue ||
                voucher.DriverTripId == filters.DriverTripId.Value)
            .Where(voucher =>
                !filters.EmployeeId.HasValue ||
                voucher.EmployeeId == filters.EmployeeId.Value)
            .Where(voucher =>
                !filters.IsDraft.HasValue ||
                filters.IsDraft.Value ==
                !voucher.IsPosted)
            .Where(voucher =>
                !filters.FromDate.HasValue ||
                voucher.VoucherDate >= filters.FromDate.Value)
            .Where(voucher =>
                !filters.ToDate.HasValue ||
                voucher.VoucherDate <= filters.ToDate.Value);

        if (filters.AccountId is int exactAccountId)
        {
            query = filters.IncludeSubAccounts
                ? query.Where(voucher =>
                    voucher.AccountId.HasValue &&
                    accountIds.Contains(voucher.AccountId.Value))
                : query.Where(voucher =>
                    voucher.AccountId == exactAccountId);
        }

        var orderedQuery = query
            .OrderByDescending(voucher => voucher.VoucherDate)
            .ThenByDescending(voucher => voucher.Id);

        // Opening balances are owned by Cashbox, not CashVoucher. They are
        // included as read-only rows in this list so the cash-voucher screen
        // has one chronological source for the opening and later movements.
        // They are never inserted into CashVouchers or into the journal here.
        var canIncludeOpeningBalances =
            !filters.CashMovementTypeId.HasValue &&
            !filters.Classification.HasValue &&
            !filters.PartyType.HasValue &&
            !filters.EmployeeId.HasValue &&
            !filters.BusinessPartnerId.HasValue &&
            !filters.DriverId.HasValue &&
            !filters.DriverTripId.HasValue &&
            !filters.AccountId.HasValue &&
            filters.IsDraft != true;

        var openingQuery = dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox => scope.HasAllCashboxes || scope.ViewCashboxIds.Contains(cashbox.Id))
            .Where(cashbox =>
                canIncludeOpeningBalances &&
                cashbox.CompanyId == companyId &&
                cashbox.OpeningBalance != 0m &&
                cashbox.OpeningBalanceDate >= fiscalYear.StartDate &&
                cashbox.OpeningBalanceDate <= fiscalYear.EndDate)
            .Where(cashbox =>
                !filters.CashboxId.HasValue ||
                cashbox.Id == filters.CashboxId.Value)
            .Where(cashbox =>
                !filters.Direction.HasValue ||
                (cashbox.OpeningBalance > 0m
                    ? CashDirection.Receipt
                    : CashDirection.Payment) == filters.Direction.Value)
            .Where(cashbox =>
                !filters.FromDate.HasValue ||
                cashbox.OpeningBalanceDate >= filters.FromDate.Value)
            .Where(cashbox =>
                !filters.ToDate.HasValue ||
                cashbox.OpeningBalanceDate <= filters.ToDate.Value)
            .Where(cashbox =>
                string.IsNullOrEmpty(search) ||
                ("OPENING-BALANCE-" + cashbox.Code).Contains(search) ||
                cashbox.Code.Contains(search) ||
                cashbox.Name.Contains(search) ||
                "رصيد افتتاحي".Contains(search) ||
                "الرصيد الافتتاحي".Contains(search))
            .Where(cashbox =>
                string.IsNullOrEmpty(voucherNumber) ||
                ("OPENING-BALANCE-" + cashbox.Code).Contains(voucherNumber));

        var realVoucherCount = await query.CountAsync(cancellationToken);
        var openingBalanceCount = canIncludeOpeningBalances
            ? await openingQuery.CountAsync(cancellationToken)
            : 0;
        var totalCount = realVoucherCount + openingBalanceCount;
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pagination.PageSize);

        // The first (offset + page size) rows from each independently ordered
        // source are sufficient to produce the same page after merging. This
        // keeps the endpoint bounded and avoids loading every voucher.
        var window = offset + pagination.PageSize;
        var take = window > int.MaxValue ? int.MaxValue : (int)window;
        var realRows = offset >= totalCount || take <= 0
            ? []
            : await orderedQuery
                .Take(take)
                .ProjectToType<CashVoucherResponse>()
                .ToListAsync(cancellationToken);

        var baseCurrency = openingBalanceCount == 0
            ? CurrencyCode.EGP
            : await dbContext.CompanySettings
                .AsNoTracking()
                .Where(setting => setting.CompanyId == companyId)
                .Select(setting => (CurrencyCode?)setting.BaseCurrency)
                .FirstOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;
        var openingRows = openingBalanceCount == 0 || offset >= totalCount
            ? []
            : (await openingQuery
                .OrderByDescending(cashbox => cashbox.OpeningBalanceDate)
                .ThenByDescending(cashbox => cashbox.Id)
                .Take(take)
                .Select(cashbox => new
                {
                    CashboxId = cashbox.Id,
                    CashboxName = cashbox.Name,
                    CashboxCode = cashbox.Code,
                    VoucherDate = cashbox.OpeningBalanceDate,
                    Direction = cashbox.OpeningBalance > 0m
                        ? CashDirection.Receipt
                        : CashDirection.Payment,
                    Amount = cashbox.OpeningBalance,
                    Currency = cashbox.Currency,
                    ExchangeRate = cashbox.OpeningExchangeRate,
                    BaseOpeningBalance = cashbox.BaseOpeningBalance,
                    CashboxBalance = cashbox.OpeningBalance +
                        cashbox.Vouchers
                            .Where(voucher =>
                                voucher.IsPosted &&
                                voucher.FiscalYearId ==
                                    fiscalYear.FiscalYearId)
                            .Sum(voucher =>
                                voucher.Direction == CashDirection.Receipt
                                    ? voucher.Amount
                                    : -voucher.Amount)
                })
                .ToListAsync(cancellationToken))
                .Select(row => new OpeningBalanceRow(
                    CashboxId: row.CashboxId,
                    CashboxName: row.CashboxName,
                    CashboxCode: row.CashboxCode,
                    VoucherDate: row.VoucherDate,
                    Direction: row.Direction,
                    Amount: row.Amount,
                    Currency: row.Currency,
                    ExchangeRate: row.ExchangeRate,
                    BaseOpeningBalance: row.BaseOpeningBalance,
                    CashboxBalance: row.CashboxBalance))
                .ToList();

        var mergedRows = realRows
            .Concat(openingRows.Select(row =>
                CreateOpeningBalanceResponse(
                    row,
                    companyId,
                    baseCurrency,
                    fiscalYear.FiscalYearId,
                    fiscalYear.FiscalYearName)))
            .OrderByDescending(row => row.VoucherDate)
            .ThenByDescending(row => row.Id)
            .Skip(offset >= int.MaxValue ? int.MaxValue : (int)offset)
            .Take(pagination.PageSize)
            .ToList();

        return Result<PagedResponse<CashVoucherResponse>>.Success(
            new PagedResponse<CashVoucherResponse>(
                Items: mergedRows,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: totalPages));
    }

    public async Task<Result<CashVoucherHandoverReportResponse>>
        GetHandoverReportAsync(
            PaginationRequest pagination,
            CashVoucherHandoverReportFilterRequest? filters = null,
            CancellationToken cancellationToken = default)
    {
        filters ??= new CashVoucherHandoverReportFilterRequest();
        var access = await CashboxPermissionGuard.ScopeAsync(cashboxAccessService, cancellationToken);
        if (access.IsFailure) return Result<CashVoucherHandoverReportResponse>.Failure(access.Errors);
        var scope = access.Value;
        if (filters.CashboxId is int selectedCashboxId)
        {
            var permission = await CashboxPermissionGuard.ViewAsync(cashboxAccessService, selectedCashboxId, cancellationToken);
            if (permission.IsFailure) return Result<CashVoucherHandoverReportResponse>.Failure(permission.Errors);
        }
        var validation = new CashVoucherHandoverReportFilterRequestValidator()
            .Validate(filters);
        if (!validation.IsValid ||
            pagination.PageNumber <= 0 ||
            pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
        {
            var errors = validation.Errors
                .Select(error => Error.Validation(
                    "CashVouchers.HandoverReportValidation",
                    error.ErrorMessage,
                    error.PropertyName))
                .ToList();
            if (pagination.PageNumber <= 0 ||
                pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
            {
                errors.Add(Error.Validation(
                    "Pagination.Invalid",
                    "بيانات الصفحات غير صحيحة."));
            }

            return Result<CashVoucherHandoverReportResponse>.Failure(errors);
        }

        var fiscalYearResult = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: filters.FiscalYearId,
            fromDate: filters.FromDate,
            toDate: filters.ToDate,
            cancellationToken: cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result<CashVoucherHandoverReportResponse>.Failure(
                fiscalYearResult.Errors);
        }

        var fiscalYear = fiscalYearResult.Value;

        var query = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.FiscalYearId == fiscalYear.FiscalYearId &&
                !voucher.IsPosted &&
                voucher.CashboxId.HasValue &&
                !voucher.InvoiceId.HasValue &&
                !voucher.CashboxTransferId.HasValue)
            .Where(voucher => scope.HasAllCashboxes || scope.ViewCashboxIds.Contains(voucher.CashboxId!.Value));

        // The official balance is always based on posted vouchers, while the
        // handover amounts are shown separately as an operational expectation.
        // Keep draft totals independent from date/direction/search filters so
        // filtering the report cannot change how much cash is expected in the
        // employee's hands. A selected cashbox only narrows the cards shown.
        var balanceRows = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox => scope.HasAllCashboxes || scope.ViewCashboxIds.Contains(cashbox.Id))
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                (!filters.CashboxId.HasValue ||
                 cashbox.Id == filters.CashboxId.Value) &&
                cashbox.Vouchers.Any(voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.FiscalYearId == fiscalYear.FiscalYearId &&
                    !voucher.IsPosted &&
                    voucher.CashboxId.HasValue &&
                    !voucher.InvoiceId.HasValue &&
                    !voucher.CashboxTransferId.HasValue))
            .Select(cashbox => new
            {
                CashboxId = cashbox.Id,
                CashboxName = cashbox.Name,
                cashbox.Currency,
                InitialOpeningBalance =
                    cashbox.OpeningBalanceDate >= fiscalYear.StartDate &&
                    cashbox.OpeningBalanceDate <= fiscalYear.EndDate
                        ? cashbox.OpeningBalance
                        : 0m,
                CarriedOpeningBalance = dbContext.JournalEntryLines
                    .Where(line =>
                        line.CompanyId == companyId &&
                        line.JournalEntry.FiscalYearId ==
                            fiscalYear.FiscalYearId &&
                        line.JournalEntry.EntryType ==
                            JournalEntryType.Opening &&
                        line.JournalEntry.SourceType ==
                            JournalEntrySourceType.FiscalYearClosing &&
                        line.JournalEntry.Status ==
                            JournalEntryStatus.Posted &&
                        line.Currency == cashbox.Currency &&
                        line.PartyType == JournalPartyType.Cashbox &&
                        line.PartyId == cashbox.Id)
                    .Select(line => (decimal?)
                        (line.TransactionDebit - line.TransactionCredit))
                    .Sum() ?? 0m,
                PostedMovement =
                    (cashbox.Vouchers
                        .Where(voucher =>
                            voucher.CompanyId == companyId &&
                            voucher.FiscalYearId ==
                                fiscalYear.FiscalYearId &&
                            voucher.IsPosted)
                        .Select(voucher => (decimal?)
                            (voucher.Direction == CashDirection.Receipt
                                ? voucher.Amount
                                : -voucher.Amount))
                        .Sum() ?? 0m),
                DraftReceipt = cashbox.Vouchers
                    .Where(voucher =>
                        voucher.CompanyId == companyId &&
                        voucher.FiscalYearId == fiscalYear.FiscalYearId &&
                        !voucher.IsPosted &&
                        voucher.CashboxId.HasValue &&
                        !voucher.InvoiceId.HasValue &&
                        !voucher.CashboxTransferId.HasValue &&
                        voucher.Direction == CashDirection.Receipt)
                    .Select(voucher => (decimal?)voucher.Amount)
                    .Sum() ?? 0m,
                DraftPayment = cashbox.Vouchers
                    .Where(voucher =>
                        voucher.CompanyId == companyId &&
                        voucher.FiscalYearId == fiscalYear.FiscalYearId &&
                        !voucher.IsPosted &&
                        voucher.CashboxId.HasValue &&
                        !voucher.InvoiceId.HasValue &&
                        !voucher.CashboxTransferId.HasValue &&
                        voucher.Direction == CashDirection.Payment)
                    .Select(voucher => (decimal?)voucher.Amount)
                    .Sum() ?? 0m
            })
            .OrderBy(row => row.CashboxName)
            .ThenBy(row => row.CashboxId)
            .ToListAsync(cancellationToken);

        var cashboxBalances = balanceRows
            .Select(row => new CashVoucherHandoverCashboxBalance(
                CashboxId: row.CashboxId,
                CashboxName: row.CashboxName,
                Currency: row.Currency,
                CurrentBalance: row.InitialOpeningBalance +
                    row.CarriedOpeningBalance + row.PostedMovement,
                DraftReceipt: row.DraftReceipt,
                DraftPayment: row.DraftPayment,
                ExpectedBalance: row.InitialOpeningBalance +
                    row.CarriedOpeningBalance + row.PostedMovement +
                    row.DraftReceipt - row.DraftPayment))
            .ToList();

        if (filters.CashboxId.HasValue)
        {
            query = query.Where(voucher =>
                voucher.CashboxId == filters.CashboxId.Value);
        }

        if (filters.FromDate.HasValue)
        {
            query = query.Where(voucher =>
                voucher.VoucherDate >= filters.FromDate.Value);
        }

        if (filters.ToDate.HasValue)
        {
            query = query.Where(voucher =>
                voucher.VoucherDate <= filters.ToDate.Value);
        }

        if (filters.Direction.HasValue)
        {
            query = query.Where(voucher =>
                voucher.Direction == filters.Direction.Value);
        }

        var search = filters.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(voucher =>
                voucher.VoucherNumber.Contains(search) ||
                (voucher.Cashbox != null &&
                 (voucher.Cashbox.Code.Contains(search) ||
                  voucher.Cashbox.Name.Contains(search))) ||
                (voucher.Description != null &&
                 voucher.Description.Contains(search)) ||
                (voucher.Notes != null && voucher.Notes.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var summaryRows = await query
            .GroupBy(voucher => voucher.Currency)
            .Select(group => new
            {
                Currency = group.Key,
                Receipt = group
                    .Where(voucher => voucher.Direction == CashDirection.Receipt)
                    .Sum(voucher => (decimal?)voucher.Amount) ?? 0m,
                Payment = group
                    .Where(voucher => voucher.Direction == CashDirection.Payment)
                    .Sum(voucher => (decimal?)voucher.Amount) ?? 0m,
                Count = group.Count()
            })
            .OrderBy(row => row.Currency)
            .ToListAsync(cancellationToken);

        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        var rows = offset >= totalCount
            ? []
            : await query
                .OrderByDescending(voucher => voucher.VoucherDate)
                .ThenByDescending(voucher => voucher.Id)
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .Select(voucher => new
                {
                    voucher.Id,
                    voucher.VoucherNumber,
                    voucher.VoucherDate,
                    voucher.Direction,
                    CashboxId = voucher.CashboxId!.Value,
                    CashboxName = voucher.Cashbox!.Name,
                    voucher.Amount,
                    voucher.Currency,
                    voucher.ExchangeRate,
                    voucher.BaseAmount,
                    voucher.Description,
                    voucher.Notes,
                    voucher.CreatedById,
                    voucher.CreatedOn
                })
                .ToListAsync(cancellationToken);

        var pageItems = rows
            .Select(row => new CashVoucherHandoverReportItemResponse(
                Id: row.Id,
                VoucherNumber: row.VoucherNumber,
                VoucherDate: row.VoucherDate,
                Direction: row.Direction,
                CashboxId: row.CashboxId,
                CashboxName: row.CashboxName,
                Amount: row.Amount,
                Currency: row.Currency,
                Description: row.Description,
                Notes: row.Notes,
                CreatedById: row.CreatedById,
                CreatedOn: row.CreatedOn,
                BaseCurrency: baseCurrency,
                ExchangeRate: row.ExchangeRate,
                BaseAmount: row.BaseAmount))
            .ToList();
        var summaries = summaryRows
            .Select(row => new CashVoucherHandoverCurrencySummary(
                Currency: row.Currency,
                Receipt: row.Receipt,
                Payment: row.Payment,
                Net: row.Receipt - row.Payment,
                Count: row.Count))
            .ToList();
        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pagination.PageSize);

        return Result<CashVoucherHandoverReportResponse>.Success(
            new CashVoucherHandoverReportResponse(
                Items: pageItems,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: totalPages,
                Summaries: summaries,
                CashboxBalances: cashboxBalances)
            {
                FiscalYearId = fiscalYear.FiscalYearId,
                FiscalYearName = fiscalYear.FiscalYearName
            });
    }

    public async Task<Result<CashVoucherResponse>> GetByIdAsync(
        int id,
        int? fiscalYearId = null,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashVoucherResponse>.Failure(InvalidId());
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: fiscalYearId,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<CashVoucherResponse>.Failure(fiscalYear.Errors);
        }

        var reference = await dbContext.CashVouchers.AsNoTracking()
            .Where(voucher => voucher.CompanyId == companyId && voucher.Id == id &&
                voucher.FiscalYearId == fiscalYear.Value.FiscalYearId)
            .Select(voucher => new
            {
                voucher.CashboxId,
                SourceCashboxId = voucher.CashboxTransfer == null ? (int?)null : voucher.CashboxTransfer.SourceCashboxId,
                DestinationCashboxId = voucher.CashboxTransfer == null ? (int?)null : voucher.CashboxTransfer.DestinationCashboxId
            }).FirstOrDefaultAsync(cancellationToken);
        if (reference is not null)
        {
            if (reference.CashboxId is null)
            {
                var access = await CashboxPermissionGuard.ScopeAsync(cashboxAccessService, cancellationToken);
                if (access.IsFailure) return Result<CashVoucherResponse>.Failure(access.Errors);
                if (!access.Value.HasAllCashboxes) return Result<CashVoucherResponse>.Failure(CashboxPermissionGuard.Forbidden());
            }
            foreach (var cashboxId in new[] { reference.CashboxId, reference.SourceCashboxId, reference.DestinationCashboxId })
            {
                if (!cashboxId.HasValue) continue;
                var access = await CashboxPermissionGuard.ViewAsync(cashboxAccessService, cashboxId.Value, cancellationToken);
                if (access.IsFailure) return Result<CashVoucherResponse>.Failure(access.Errors);
            }
        }
        var response = await ProjectResponseQuery(
                id,
                fiscalYear.Value.FiscalYearId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<CashVoucherResponse>.Failure(NotFound(id))
            : Result<CashVoucherResponse>.Success(response);
    }

    public async Task<Result<CashVoucherPartySelectResponse>>
        GetPartySelectAsync(
            CancellationToken cancellationToken = default)
    {
        var businessPartnerRows = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(partner =>
                partner.CompanyId == companyId &&
                partner.IsActive)
            .OrderBy(partner => partner.Name)
            .ThenBy(partner => partner.Id)
            .Select(partner => new
            {
                partner.Id,
                partner.Name
            })
            .ToListAsync(cancellationToken);

        var driverRows = await dbContext.Drivers
            .AsNoTracking()
            .Where(driver =>
                driver.CompanyId == companyId &&
                driver.IsActive)
            .OrderBy(driver => driver.Name)
            .ThenBy(driver => driver.Id)
            .Select(driver => new
            {
                driver.Id,
                driver.Name
            })
            .ToListAsync(cancellationToken);

        var employeeRows = await dbContext.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.CompanyId == companyId &&
                employee.IsActive)
            .OrderBy(employee => employee.Name)
            .ThenBy(employee => employee.Id)
            .Select(employee => new
            {
                employee.Id,
                employee.Name
            })
            .ToListAsync(cancellationToken);

        var accountRows = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.CompanyId == companyId &&
                account.IsActive &&
                account.IsPosting &&
                (account.AccountType == AccountType.Expense ||
                 account.AccountType == AccountType.Revenue))
            .OrderBy(account => account.Name)
            .ThenBy(account => account.Id)
            .Select(account => new
            {
                account.Id,
                account.Code,
                account.Name,
                account.AccountType
            })
            .ToListAsync(cancellationToken);

        var response = new CashVoucherPartySelectResponse(
            BusinessPartners: businessPartnerRows
                .Select(partner => new SelectResponse(
                    Id: partner.Id,
                    Name: partner.Name))
                .ToList(),
            Drivers: driverRows
                .Select(driver => new SelectResponse(
                    Id: driver.Id,
                    Name: driver.Name))
                .ToList(),
            Employees: employeeRows
                .Select(employee => new SelectResponse(
                    Id: employee.Id,
                    Name: employee.Name))
                .ToList(),
            Expenses: accountRows
                .Where(account => account.AccountType == AccountType.Expense)
                .Select(account =>
                    new CashVoucherAccountSelectResponse(
                        Id: account.Id,
                        Name: account.Name,
                        Classification: CashMovementClassification.Expense,
                        Code: account.Code,
                        AccountType: account.AccountType))
                .ToList(),
            Revenues: accountRows
                .Where(account => account.AccountType == AccountType.Revenue)
                .Select(account =>
                    new CashVoucherAccountSelectResponse(
                        Id: account.Id,
                        Name: account.Name,
                        Classification: CashMovementClassification.Revenue,
                        Code: account.Code,
                        AccountType: account.AccountType))
                .ToList());

        return Result<CashVoucherPartySelectResponse>.Success(response);
    }
}
