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

namespace MiniErp.Infrastructure.Services.CashVouchers;

public sealed class CashVoucherService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IExchangeRateResolver exchangeRateResolver,
    TimeProvider timeProvider,
    ICashVoucherPostingService cashVoucherPostingService,
    IFiscalYearPeriodGuard? fiscalYearPeriodGuard = null)
    : ICashVoucherService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

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
            .Where(voucher => voucher.CompanyId == companyId)
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
                (voucher.DriverTrip != null &&
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
            .Where(cashbox =>
                canIncludeOpeningBalances &&
                cashbox.CompanyId == companyId &&
                cashbox.OpeningBalance != 0m)
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
                    BaseOpeningBalance = cashbox.BaseOpeningBalance
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
                    BaseOpeningBalance: row.BaseOpeningBalance))
                .ToList();

        var mergedRows = realRows
            .Concat(openingRows.Select(row =>
                CreateOpeningBalanceResponse(row, companyId, baseCurrency)))
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

        var query = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                !voucher.IsPosted &&
                voucher.CashboxId.HasValue &&
                !voucher.InvoiceId.HasValue &&
                !voucher.CashboxTransferId.HasValue);

        // The official balance is always based on posted vouchers, while the
        // handover amounts are shown separately as an operational expectation.
        // Keep draft totals independent from date/direction/search filters so
        // filtering the report cannot change how much cash is expected in the
        // employee's hands. A selected cashbox only narrows the cards shown.
        var balanceRows = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                (!filters.CashboxId.HasValue ||
                 cashbox.Id == filters.CashboxId.Value) &&
                cashbox.Vouchers.Any(voucher =>
                    voucher.CompanyId == companyId &&
                    !voucher.IsPosted &&
                    voucher.CashboxId.HasValue &&
                    !voucher.InvoiceId.HasValue &&
                    !voucher.CashboxTransferId.HasValue))
            .Select(cashbox => new
            {
                CashboxId = cashbox.Id,
                CashboxName = cashbox.Name,
                cashbox.Currency,
                CurrentBalance = cashbox.OpeningBalance +
                    (cashbox.Vouchers
                        .Where(voucher =>
                            voucher.CompanyId == companyId &&
                            voucher.IsPosted)
                        .Select(voucher => (decimal?)
                            (voucher.Direction == CashDirection.Receipt
                                ? voucher.Amount
                                : -voucher.Amount))
                        .Sum() ?? 0m),
                DraftReceipt = cashbox.Vouchers
                    .Where(voucher =>
                        voucher.CompanyId == companyId &&
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
                CurrentBalance: row.CurrentBalance,
                DraftReceipt: row.DraftReceipt,
                DraftPayment: row.DraftPayment,
                ExpectedBalance: row.CurrentBalance +
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
                CreatedOn: row.CreatedOn))
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
                CashboxBalances: cashboxBalances));
    }

    public async Task<Result<CashVoucherResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashVoucherResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
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

    public async Task<Result<CashVoucherResponse>> AddAsync(
        CashVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.VoucherDate,
                nameof(CashVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        var cashbox = await dbContext.Cashboxes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == request.CashboxId,
                cancellationToken);
        if (cashbox is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CashVoucherResponse>.Failure(
                CashboxNotFound(request.CashboxId));
        }

        if (!cashbox.IsActive)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CashVoucherResponse>.Failure(CashboxInactive());
        }

        var prefix = request.Direction == CashDirection.Receipt
            ? "RCV"
            : "PAY";
        var voucherNumber = await EntityIdentifierGenerator
            .GenerateUniqueAsync(
                dbContext,
                prefix,
                companyId,
                dbContext.CashVouchers
                    .IgnoreQueryFilters()
                    .Where(entity => entity.CompanyId == companyId)
                    .Select(entity => entity.VoucherNumber),
                cancellationToken);
        var voucher = new CashVoucher
        {
            CompanyId = companyId,
            VoucherNumber = voucherNumber,
            VoucherDate = request.VoucherDate,
            Direction = request.Direction,
            CashboxId = cashbox.Id,
            PartyType = CashPartyType.None,
            Classification = null,
            AccountId = null,
            CashMovementTypeId = null,
            EmployeeId = null,
            BusinessPartnerId = null,
            DriverId = null,
            DriverTripId = null,
            ExternalPartyName = null,
            Amount = request.Amount,
            ReferenceNumber = null,
            Description = NormalizeText(request.Description),
            Notes = NormalizeText(request.Notes),
            IsPosted = false
        };
        voucher.InitializeDraft(cashbox.Currency);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);

        dbContext.CashVouchers.Add(voucher);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Drafts are intentionally operational only. Do not create partner /
        // employee movements or a journal entry; completion via PUT performs
        // those side effects atomically.

        var response = await ProjectResponseQuery(voucher.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<CashVoucherResponse>.Success(response);
    }

    public async Task<Result<CashVoucherBulkResponse>> BulkAsync(
        CashVoucherBulkRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = new CashVoucherBulkRequestValidator()
            .Validate(request);
        if (!validationResult.IsValid)
        {
            return Result<CashVoucherBulkResponse>.Failure(
                validationResult.Errors.Select(error =>
                    Error.Validation(
                        "CashVouchers.BulkValidation",
                        error.ErrorMessage,
                        error.PropertyName)));
        }

        // A bulk request is an independent unit of work. Clearing snapshots from
        // earlier CRUD calls ensures RowVersion checks use the database value.
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var results = new List<CashVoucherBulkItemResponse>();

        for (var index = 0; index < request.Items!.Count; index++)
        {
            var item = request.Items[index];
            Result<CashVoucherBulkItemResponse> itemResult = item switch
            {
                CashVoucherBulkAddItemRequest add => await AddBulkItemAsync(
                    add,
                    cancellationToken),
                CashVoucherBulkUpdateItemRequest update => await UpdateBulkItemAsync(
                    update,
                    cancellationToken),
                CashVoucherBulkDeleteItemRequest delete => await DeleteBulkItemAsync(
                    delete,
                    cancellationToken),
                _ => Result<CashVoucherBulkItemResponse>.Failure(
                    Error.Validation(
                        "CashVouchers.BulkInvalidAction",
                        "نوع العملية المرسل غير صحيح."))
            };

            if (itemResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<CashVoucherBulkResponse>.Failure(
                    itemResult.Errors.Select(error =>
                        BulkItemFailure(
                            index,
                            error,
                            isVoucherPayloadError:
                                item is CashVoucherBulkAddItemRequest or
                                    CashVoucherBulkUpdateItemRequest)));
            }

            results.Add(itemResult.Value);
        }

        await transaction.CommitAsync(cancellationToken);

        var summary = new CashVoucherBulkSummary(
            Added: results.Count(item => item.Action == CashVoucherBulkAction.Add),
            Updated: results.Count(item => item.Action == CashVoucherBulkAction.Update),
            Deleted: results.Count(item => item.Action == CashVoucherBulkAction.Delete));
        return Result<CashVoucherBulkResponse>.Success(
            new CashVoucherBulkResponse(
                Items: results,
                Summary: summary));
    }

    public async Task<Result<CashVoucherResponse>> UpdateAsync(
        int id,
        CashVoucherUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashVoucherResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<CashVoucherResponse>.Failure(
                RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var voucher = await dbContext.CashVouchers.FirstOrDefaultAsync(
            entity =>
                entity.Id == id &&
                entity.CompanyId == companyId,
            cancellationToken);
        if (voucher is null)
        {
            return Result<CashVoucherResponse>.Failure(NotFound(id));
        }

        if (!voucher.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<CashVoucherResponse>.Failure(Concurrency());
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                voucher.VoucherDate,
                nameof(CashVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherResponse>.Failure(
                    fiscalYearResult.Errors);
            }

            fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.VoucherDate,
                nameof(CashVoucherUpdateRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        if (voucher.InvoiceId.HasValue)
        {
            return Result<CashVoucherResponse>.Failure(
                InvoiceGeneratedReadOnly());
        }

        if (voucher.CashboxTransferId.HasValue)
        {
            return Result<CashVoucherResponse>.Failure(
                TransferGeneratedReadOnly());
        }

        request = await NormalizeEmployeeMovementTypeAsync(
            request,
            voucher,
            cancellationToken);

        var preparation = await PrepareAsync(
            request,
            voucher,
            enforceManualPostingTarget: true,
            cancellationToken: cancellationToken);
        if (preparation.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CashVoucherResponse>.Failure(preparation.Error);
        }

        var entry = dbContext.Entry(voucher);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion!;

        request.Adapt(voucher);
        voucher.PartyType = preparation.Value.PartyType;
        voucher.Classification = preparation.Value.Classification;
        voucher.IsPosted = true;
        await ApplyPreparationAsync(
            voucher,
            preparation.Value,
            cancellationToken);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(entity => entity.LastModifiedAt).IsModified = true;

        try
        {
            await SynchronizePartnerMovementAsync(
                voucher,
                preparation.Value.BusinessPartner is not null,
                cancellationToken);
            await SynchronizeEmployeeMovementAsync(
                voucher,
                preparation.Value.PartyType == CashPartyType.Employee,
                request.EmployeeMovementType,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var postingResult = await cashVoucherPostingService
                .SynchronizeAsync(voucher, cancellationToken);
            if (postingResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<CashVoucherResponse>.Failure(
                    postingResult.Errors);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<CashVoucherResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<CashVoucherResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var voucher = await dbContext.CashVouchers.FirstOrDefaultAsync(
            entity =>
                entity.Id == id &&
                entity.CompanyId == companyId,
            cancellationToken);
        if (voucher is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                voucher.VoucherDate,
                nameof(CashVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result.Failure(fiscalYearResult.Errors);
            }
        }

        if (voucher.InvoiceId.HasValue)
        {
            return Result.Failure(InvoiceGeneratedReadOnly());
        }

        if (voucher.CashboxTransferId.HasValue)
        {
            return Result.Failure(TransferGeneratedReadOnly());
        }

        var balanceError = await ValidateFinalBalancesAsync(
            voucher,
            proposedCashboxId: null,
            proposedDirection: null,
            proposedAmount: null,
            cancellationToken);
        if (balanceError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(balanceError);
        }

        try
        {
            var partnerMovements = await dbContext.BusinessPartnerMovements
                .Where(movement =>
                    movement.CompanyId == companyId &&
                    movement.CashVoucherId == id)
                .ToListAsync(cancellationToken);
            dbContext.BusinessPartnerMovements.RemoveRange(partnerMovements);
            await SynchronizeEmployeeMovementAsync(
                voucher,
                shouldExist: false,
                movementType: null,
                cancellationToken);
            dbContext.CashVouchers.Remove(voucher);
            await dbContext.SaveChangesAsync(cancellationToken);

            var postingResult = await cashVoucherPostingService.DeleteAsync(
                voucher.Id,
                cancellationToken);
            if (postingResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result.Failure(postingResult.Errors);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }
    }

    private async Task<Result<CashVoucherBulkItemResponse>> AddBulkItemAsync(
        CashVoucherBulkAddItemRequest item,
        CancellationToken cancellationToken)
    {
        var request = ToUpdateRequest(item.Voucher!, rowVersion: null);
        request = await NormalizeEmployeeMovementTypeAsync(
            request,
            currentVoucher: null,
            cancellationToken: cancellationToken);
        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.VoucherDate,
                nameof(CashVoucherBulkVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherBulkItemResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        var preparation = await PrepareAsync(
            request,
            currentVoucher: null,
            // Bulk keeps its legacy behavior and may contain an
            // unclassified posted cash movement.
            enforceManualPostingTarget: false,
            cancellationToken: cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                preparation.Errors);
        }

        var voucher = new CashVoucher
        {
            CompanyId = companyId,
            VoucherNumber = await GenerateVoucherNumberAsync(
                request.Direction,
                cancellationToken)
        };
        request.Adapt(voucher);
        voucher.PartyType = preparation.Value.PartyType;
        voucher.Classification = preparation.Value.Classification;
        voucher.IsPosted = true;
        await ApplyPreparationAsync(voucher, preparation.Value, cancellationToken);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);

        dbContext.CashVouchers.Add(voucher);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SynchronizePartnerMovementAsync(
            voucher,
            preparation.Value.BusinessPartner is not null,
            cancellationToken);
        await SynchronizeEmployeeMovementAsync(
            voucher,
            preparation.Value.PartyType == CashPartyType.Employee,
            request.EmployeeMovementType,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var postingResult = await cashVoucherPostingService
            .SynchronizeAsync(voucher, cancellationToken);
        if (postingResult.IsFailure)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                postingResult.Errors);
        }

        var response = await ProjectResponseQuery(voucher.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        return Result<CashVoucherBulkItemResponse>.Success(
            new CashVoucherBulkItemResponse(
                Action: CashVoucherBulkAction.Add,
                Status: "Added",
                Id: voucher.Id,
                Voucher: response));
    }

    private async Task<Result<CashVoucherBulkItemResponse>> UpdateBulkItemAsync(
        CashVoucherBulkUpdateItemRequest item,
        CancellationToken cancellationToken)
    {
        var id = item.Id;
        var voucher = await dbContext.CashVouchers.FirstOrDefaultAsync(
            entity =>
                entity.Id == id &&
                entity.CompanyId == companyId,
            cancellationToken);
        if (voucher is null)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                NotFound(id));
        }

        if (!voucher.RowVersion.SequenceEqual(item.RowVersion!))
        {
            return Result<CashVoucherBulkItemResponse>.Failure(Concurrency());
        }

        var request = ToUpdateRequest(item.Voucher!, item.RowVersion);
        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                voucher.VoucherDate,
                nameof(CashVoucherBulkVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherBulkItemResponse>.Failure(
                    fiscalYearResult.Errors);
            }

            fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.VoucherDate,
                nameof(CashVoucherBulkVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherBulkItemResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        if (voucher.InvoiceId.HasValue)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                InvoiceGeneratedReadOnly());
        }

        if (voucher.CashboxTransferId.HasValue)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                TransferGeneratedReadOnly());
        }

        request = await NormalizeEmployeeMovementTypeAsync(
            request,
            voucher,
            cancellationToken);

        var preparation = await PrepareAsync(
            request,
            voucher,
            // Bulk keeps its legacy behavior and may contain an
            // unclassified posted cash movement.
            enforceManualPostingTarget: false,
            cancellationToken: cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                preparation.Errors);
        }

        var entry = dbContext.Entry(voucher);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            item.RowVersion!;
        request.Adapt(voucher);
        voucher.PartyType = preparation.Value.PartyType;
        voucher.Classification = preparation.Value.Classification;
        voucher.IsPosted = true;
        await ApplyPreparationAsync(voucher, preparation.Value, cancellationToken);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(entity => entity.LastModifiedAt).IsModified = true;

        try
        {
            await SynchronizePartnerMovementAsync(
                voucher,
                preparation.Value.BusinessPartner is not null,
                cancellationToken);
            await SynchronizeEmployeeMovementAsync(
                voucher,
                preparation.Value.PartyType == CashPartyType.Employee,
                request.EmployeeMovementType,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var postingResult = await cashVoucherPostingService
                .SynchronizeAsync(voucher, cancellationToken);
            if (postingResult.IsFailure)
            {
                return Result<CashVoucherBulkItemResponse>.Failure(
                    postingResult.Errors);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<CashVoucherBulkItemResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(voucher.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        return Result<CashVoucherBulkItemResponse>.Success(
            new CashVoucherBulkItemResponse(
                Action: CashVoucherBulkAction.Update,
                Status: "Updated",
                Id: voucher.Id,
                Voucher: response));
    }

    private async Task<Result<CashVoucherBulkItemResponse>> DeleteBulkItemAsync(
        CashVoucherBulkDeleteItemRequest item,
        CancellationToken cancellationToken)
    {
        var id = item.Id;
        var voucher = await dbContext.CashVouchers.FirstOrDefaultAsync(
            entity =>
                entity.Id == id &&
                entity.CompanyId == companyId,
            cancellationToken);
        if (voucher is null)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                NotFound(id));
        }

        if (!voucher.RowVersion.SequenceEqual(item.RowVersion!))
        {
            return Result<CashVoucherBulkItemResponse>.Failure(Concurrency());
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                voucher.VoucherDate,
                nameof(CashVoucherBulkVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherBulkItemResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        if (voucher.InvoiceId.HasValue)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                InvoiceGeneratedReadOnly());
        }

        if (voucher.CashboxTransferId.HasValue)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                TransferGeneratedReadOnly());
        }

        var balanceError = await ValidateFinalBalancesAsync(
            voucher,
            proposedCashboxId: null,
            proposedDirection: null,
            proposedAmount: null,
            cancellationToken);
        if (balanceError is not null)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(balanceError);
        }

        var entry = dbContext.Entry(voucher);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            item.RowVersion!;

        try
        {
            var partnerMovements = await dbContext.BusinessPartnerMovements
                .Where(movement =>
                    movement.CompanyId == companyId &&
                    movement.CashVoucherId == voucher.Id)
                .ToListAsync(cancellationToken);
            dbContext.BusinessPartnerMovements.RemoveRange(partnerMovements);
            await SynchronizeEmployeeMovementAsync(
                voucher,
                shouldExist: false,
                movementType: null,
                cancellationToken);
            dbContext.CashVouchers.Remove(voucher);
            await dbContext.SaveChangesAsync(cancellationToken);

            var postingResult = await cashVoucherPostingService.DeleteAsync(
                voucher.Id,
                cancellationToken);
            if (postingResult.IsFailure)
            {
                return Result<CashVoucherBulkItemResponse>.Failure(
                    postingResult.Errors);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<CashVoucherBulkItemResponse>.Failure(Concurrency());
        }

        return Result<CashVoucherBulkItemResponse>.Success(
            new CashVoucherBulkItemResponse(
                Action: CashVoucherBulkAction.Delete,
                Status: "Deleted",
                Id: voucher.Id,
                Voucher: null));
    }

    private async Task<string> GenerateVoucherNumberAsync(
        CashDirection direction,
        CancellationToken cancellationToken)
    {
        var prefix = direction == CashDirection.Receipt ? "RCV" : "PAY";
        return await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix,
            companyId,
            dbContext.CashVouchers
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.VoucherNumber),
            cancellationToken);
    }

    private static CashVoucherUpdateRequest ToUpdateRequest(
        CashVoucherBulkVoucherRequest request,
        byte[]? rowVersion) =>
        new(
            VoucherDate: request.VoucherDate,
            Direction: request.Direction,
            CashboxId: request.CashboxId,
            CashMovementTypeId: request.CashMovementTypeId,
            EmployeeId: request.EmployeeId,
            BusinessPartnerId: request.BusinessPartnerId,
            DriverId: request.DriverId,
            DriverTripId: request.DriverTripId,
            ExternalPartyName: request.ExternalPartyName,
            Amount: request.Amount,
            ReferenceNumber: request.ReferenceNumber,
            Description: request.Description,
            Notes: request.Notes,
            RowVersion: rowVersion,
            ExchangeRate: request.ExchangeRate,
            AccountId: request.AccountId,
            EmployeeMovementType: request.EmployeeMovementType);

    private async Task<CashVoucherUpdateRequest> NormalizeEmployeeMovementTypeAsync(
        CashVoucherUpdateRequest request,
        CashVoucher? currentVoucher,
        CancellationToken cancellationToken)
    {
        if (!request.EmployeeId.HasValue ||
            request.EmployeeMovementType.HasValue)
        {
            return request;
        }

        EmployeeMovementType? existingType = null;
        if (currentVoucher is not null)
        {
            existingType = await dbContext.EmployeeMovements
                .AsNoTracking()
                .Where(movement =>
                    movement.CompanyId == companyId &&
                    movement.CashVoucherId == currentVoucher.Id)
                .Select(movement => (EmployeeMovementType?)movement.Type)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var isReceipt = request.Direction == CashDirection.Receipt;
        var effectiveType = existingType.HasValue &&
            EmployeeAccountRules.IsCreditMovement(existingType.Value) ==
            isReceipt
                ? existingType.Value
                : GetDefaultEmployeeMovementType(request.Direction);

        return request with { EmployeeMovementType = effectiveType };
    }

    private static EmployeeMovementType GetDefaultEmployeeMovementType(
        CashDirection direction) =>
        direction == CashDirection.Receipt
            ? EmployeeMovementType.Credit
            : EmployeeMovementType.Advance;

    private async Task<Result<VoucherPreparation>> PrepareAsync(
        CashVoucherUpdateRequest request,
        CashVoucher? currentVoucher,
        bool enforceManualPostingTarget,
        CancellationToken cancellationToken)
    {
        if (!request.CashboxId.HasValue)
        {
            return Result<VoucherPreparation>.Failure(
                PostingReferencesMustBeTogether());
        }

        var cashboxId = request.CashboxId.Value;
        if (enforceManualPostingTarget &&
            !HasExactlyOnePostingTarget(request))
        {
            return Result<VoucherPreparation>.Failure(
                PartySelectionMustBeExclusive());
        }

        if (!enforceManualPostingTarget && !HasAtMostOneTarget(request))
        {
            return Result<VoucherPreparation>.Failure(
                PartySelectionMustBeExclusive());
        }

        if (request.DriverTripId.HasValue && !request.DriverId.HasValue)
        {
            return Result<VoucherPreparation>.Failure(
                DriverTripRequiresDriver());
        }

        var partyType = DerivePartyType(request);

        if (partyType == CashPartyType.Employee)
        {
            if (!request.EmployeeMovementType.HasValue)
            {
                return Result<VoucherPreparation>.Failure(
                    EmployeeMovementTypeRequired());
            }

            if (!Enum.IsDefined(request.EmployeeMovementType.Value))
            {
                return Result<VoucherPreparation>.Failure(
                    EmployeeMovementTypeInvalid());
            }

            var isReceipt = request.Direction == CashDirection.Receipt;
            if (EmployeeAccountRules.IsCreditMovement(
                    request.EmployeeMovementType.Value) != isReceipt)
            {
                return Result<VoucherPreparation>.Failure(
                    EmployeeMovementTypeDirectionMismatch());
            }
        }
        else if (request.EmployeeMovementType.HasValue)
        {
            return Result<VoucherPreparation>.Failure(
                EmployeeMovementTypeNotAllowed());
        }

        var cashbox = await dbContext.Cashboxes
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == cashboxId,
                cancellationToken);
        if (cashbox is null)
        {
            return Result<VoucherPreparation>.Failure(
                CashboxNotFound(cashboxId));
        }

        if (!cashbox.IsActive &&
            (currentVoucher is null ||
             currentVoucher.CashboxId != cashbox.Id))
        {
            return Result<VoucherPreparation>.Failure(CashboxInactive());
        }

        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            cashbox.Currency,
            request.VoucherDate,
            request.ExchangeRate,
            cancellationToken);
        if (exchangeRateResult.IsFailure)
        {
            return Result<VoucherPreparation>.Failure(
                exchangeRateResult.Error);
        }

        CashMovementType? movementType = null;
        if (request.CashMovementTypeId is int cashMovementTypeId)
        {
            movementType = await dbContext.CashMovementTypes
                .FirstOrDefaultAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == cashMovementTypeId,
                    cancellationToken);
            if (movementType is null)
            {
                return Result<VoucherPreparation>.Failure(
                    MovementTypeNotFound(cashMovementTypeId));
            }

            if (!movementType.IsActive &&
                (enforceManualPostingTarget ||
                 currentVoucher is null ||
                 currentVoucher.CashMovementTypeId != movementType.Id))
            {
                return Result<VoucherPreparation>.Failure(
                    MovementTypeInactive());
            }

            if (movementType.Direction != request.Direction)
            {
                return Result<VoucherPreparation>.Failure(
                    MovementTypeDirectionMismatch());
            }

        }

        Account? account = null;
        if (request.AccountId is int accountId)
        {
            account = await dbContext.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == accountId,
                    cancellationToken);
            if (account is null)
            {
                return Result<VoucherPreparation>.Failure(
                    AccountNotFound(accountId));
            }

            if (!account.IsActive || !account.IsPosting)
            {
                return Result<VoucherPreparation>.Failure(
                    AccountInactiveOrNotPosting());
            }

            var accountMatchesDirection =
                (request.Direction == CashDirection.Payment &&
                 account.AccountType == AccountType.Expense) ||
                (request.Direction == CashDirection.Receipt &&
                 account.AccountType == AccountType.Revenue);
            if (!accountMatchesDirection)
            {
                return Result<VoucherPreparation>.Failure(
                    AccountDirectionMismatch());
            }
        }

        BusinessPartner? partner = null;
        if (partyType == CashPartyType.Partner)
        {
            partner = await dbContext.BusinessPartners
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == request.BusinessPartnerId &&
                        entity.IsActive,
                    cancellationToken);
            if (partner is null)
            {
                return Result<VoucherPreparation>.Failure(
                    PartnerNotFound(request.BusinessPartnerId));
            }

            if (partner.Currency != cashbox.Currency)
            {
                return Result<VoucherPreparation>.Failure(
                    PartnerCurrencyMismatch());
            }
        }
        else if (movementType is not null &&
                 movementType.PartnerEffect != PartnerAccountEffect.None)
        {
            return Result<VoucherPreparation>.Failure(
                MovementTypeForPartnerOnly());
        }

        if (partyType == CashPartyType.Employee)
        {
            var employeeExists = await dbContext.Employees
                .AsNoTracking()
                .AnyAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == request.EmployeeId &&
                        entity.IsActive,
                    cancellationToken);
            if (!employeeExists)
            {
                return Result<VoucherPreparation>.Failure(
                    EmployeeNotFound(request.EmployeeId));
            }
        }

        Driver? driver = null;
        if (partyType == CashPartyType.Driver)
        {
            driver = await dbContext.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == request.DriverId &&
                        entity.IsActive,
                    cancellationToken);
            if (driver is null)
            {
                return Result<VoucherPreparation>.Failure(
                    DriverNotFound(request.DriverId));
            }

            if (request.DriverTripId.HasValue)
            {
                var tripExists = await dbContext.DriverTrips
                    .AsNoTracking()
                    .AnyAsync(
                        trip =>
                            trip.CompanyId == companyId &&
                            trip.Id == request.DriverTripId.Value &&
                            trip.DriverId == driver.Id,
                        cancellationToken);
                if (!tripExists)
                {
                    return Result<VoucherPreparation>.Failure(
                        DriverTripNotFound(request.DriverTripId.Value));
                }
            }
        }

        var balanceError = await ValidateFinalBalancesAsync(
            currentVoucher,
            proposedCashboxId: cashboxId,
            proposedDirection: request.Direction,
            proposedAmount: request.Amount,
            cancellationToken);
        if (balanceError is not null)
        {
            return Result<VoucherPreparation>.Failure(balanceError);
        }

        return Result<VoucherPreparation>.Success(
            new VoucherPreparation(
                Cashbox: cashbox,
                PartyType: partyType,
                BusinessPartner: partner,
                Driver: driver,
                ExchangeRate: exchangeRateResult.Value,
                Classification: account?.AccountType switch
                    {
                        AccountType.Expense =>
                            CashMovementClassification.Expense,
                        AccountType.Revenue =>
                            CashMovementClassification.Revenue,
                        _ => movementType?.Classification
                    }));
    }

    private async Task<Error?> ValidateFinalBalancesAsync(
        CashVoucher? currentVoucher,
        int? proposedCashboxId,
        CashDirection? proposedDirection,
        decimal? proposedAmount,
        CancellationToken cancellationToken)
    {
        var affectedCashboxIds = new HashSet<int>();
        if (currentVoucher is
            {
                CashboxId: int currentCashboxId
            } &&
            currentVoucher.IsPosted)
        {
            affectedCashboxIds.Add(currentCashboxId);
        }

        if (proposedCashboxId.HasValue)
        {
            affectedCashboxIds.Add(proposedCashboxId.Value);
        }

        foreach (var cashboxId in affectedCashboxIds)
        {
            var excludedVoucherId = currentVoucher?.Id;
            var balance = await dbContext.Cashboxes
                .AsNoTracking()
                .Where(cashbox =>
                    cashbox.CompanyId == companyId &&
                    cashbox.Id == cashboxId)
                .Select(cashbox =>
                    cashbox.OpeningBalance +
                    (cashbox.Vouchers
                        .Where(voucher =>
                            voucher.IsPosted &&
                            (!excludedVoucherId.HasValue ||
                             voucher.Id != excludedVoucherId.Value))
                        .Sum(voucher =>
                            (decimal?)(voucher.Direction ==
                                CashDirection.Receipt
                                ? voucher.Amount
                                : -voucher.Amount)) ?? 0m))
                .SingleAsync(cancellationToken);

            if (proposedCashboxId == cashboxId &&
                proposedDirection.HasValue &&
                proposedAmount.HasValue)
            {
                balance += proposedDirection == CashDirection.Receipt
                    ? proposedAmount.Value
                    : -proposedAmount.Value;
            }

            if (balance < 0m)
            {
                return InsufficientCashboxBalance(cashboxId);
            }
        }

        return null;
    }

    private BusinessPartnerMovement CreatePartnerMovement(
        CashVoucher voucher)
    {
        var debit = voucher.Direction == CashDirection.Payment
            ? voucher.Amount
            : 0m;
        var credit = voucher.Direction == CashDirection.Receipt
            ? voucher.Amount
            : 0m;

        var movement = new BusinessPartnerMovement
        {
            CompanyId = companyId,
            BusinessPartnerId = voucher.BusinessPartnerId!.Value,
            CashVoucherId = voucher.Id,
            CashVoucher = voucher,
            MovementType = voucher.Direction == CashDirection.Receipt
                ? BusinessPartnerMovementType.CashReceipt
                : BusinessPartnerMovementType.CashPayment,
            MovementDate = voucher.VoucherDate,
            Currency = voucher.Currency,
            Debit = debit,
            Credit = credit,
            Description = voucher.Description ??
                $"Cash voucher {voucher.VoucherNumber}"
        };
        movement.ApplyExchangeRate(voucher.ExchangeRate);
        return movement;
    }

    private async Task SynchronizePartnerMovementAsync(
        CashVoucher voucher,
        bool shouldExist,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.BusinessPartnerMovements
            .FirstOrDefaultAsync(
                movement =>
                movement.CompanyId == companyId &&
                movement.CashVoucherId == voucher.Id,
                cancellationToken);

        if (!shouldExist)
        {
            if (existing is not null)
            {
                dbContext.BusinessPartnerMovements.Remove(existing);
            }

            return;
        }

        if (existing is null)
        {
            dbContext.BusinessPartnerMovements.Add(
                CreatePartnerMovement(voucher));
            return;
        }

        existing.BusinessPartnerId = voucher.BusinessPartnerId!.Value;
        existing.MovementType =
            voucher.Direction == CashDirection.Receipt
                ? BusinessPartnerMovementType.CashReceipt
                : BusinessPartnerMovementType.CashPayment;
        existing.MovementDate = voucher.VoucherDate;
        existing.Currency = voucher.Currency;
        existing.Debit = voucher.Direction == CashDirection.Payment
            ? voucher.Amount
            : 0m;
        existing.Credit = voucher.Direction == CashDirection.Receipt
            ? voucher.Amount
            : 0m;
        existing.ApplyExchangeRate(voucher.ExchangeRate);
        existing.Description = voucher.Description ??
            $"Cash voucher {voucher.VoucherNumber}";
    }

    private async Task SynchronizeEmployeeMovementAsync(
        CashVoucher voucher,
        bool shouldExist,
        EmployeeMovementType? movementType,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.EmployeeMovements
            .FirstOrDefaultAsync(
                movement =>
                    movement.CompanyId == companyId &&
                    movement.CashVoucherId == voucher.Id,
                cancellationToken);

        if (!shouldExist || voucher.EmployeeId is null)
        {
            if (existing is not null)
            {
                dbContext.EmployeeMovements.Remove(existing);
            }

            return;
        }

        // Keep a safe fallback for internal callers and existing vouchers so
        // synchronization remains idempotent when they do not send a type.
        var effectiveType = movementType ??
            existing?.Type ??
            GetDefaultEmployeeMovementType(voucher.Direction);

        if (existing is null)
        {
            existing = new EmployeeMovement
            {
                CompanyId = companyId,
                CashVoucherId = voucher.Id
            };
            dbContext.EmployeeMovements.Add(existing);
        }

        existing.EmployeeId = voucher.EmployeeId.Value;
        existing.MovementDate = voucher.VoucherDate;
        existing.Currency = voucher.Currency;
        existing.Notes = string.IsNullOrWhiteSpace(voucher.Notes)
            ? null
            : voucher.Notes.Trim();
        existing.ApplyAmounts(effectiveType, voucher.Amount);
        existing.ApplyExchangeRate(voucher.ExchangeRate);
    }

    private static CashVoucherResponse CreateOpeningBalanceResponse(
        OpeningBalanceRow row,
        int companyId,
        CurrencyCode baseCurrency)
    {
        var amount = Math.Abs(row.Amount);
        var exchangeRate = row.Currency == baseCurrency
            ? 1m
            : row.ExchangeRate > 0m
                ? row.ExchangeRate
                : 1m;
        var baseAmount = row.Currency == baseCurrency
            ? amount
            : Math.Abs(row.BaseOpeningBalance) > 0m
                ? Math.Abs(row.BaseOpeningBalance)
                : ExchangeRateRules.ConvertToBase(amount, exchangeRate);

        return new CashVoucherResponse(
            Id: -row.CashboxId,
            CompanyId: companyId,
            VoucherNumber: $"OPENING-BALANCE-{row.CashboxCode}",
            VoucherDate: row.VoucherDate,
            Direction: row.Direction,
            CashboxId: row.CashboxId,
            CashboxName: row.CashboxName,
            CashMovementTypeId: null,
            CashMovementTypeName: "رصيد افتتاحي",
            Classification: null,
            PartyType: CashPartyType.None,
            EmployeeId: null,
            EmployeeName: null,
            BusinessPartnerId: null,
            BusinessPartnerName: null,
            DriverId: null,
            DriverName: null,
            DriverTripId: null,
            DriverTripInvoiceNumber: null,
            ExternalPartyName: null,
            Amount: amount,
            Currency: row.Currency,
            BaseCurrency: baseCurrency,
            ExchangeRate: exchangeRate,
            BaseAmount: baseAmount,
            ReferenceNumber: null,
            Description: "الرصيد الافتتاحي",
            Notes: null,
            RowVersion: [])
        {
            IsDraft = false,
            IsOpeningBalance = true
        };
    }

    private sealed record OpeningBalanceRow(
        int CashboxId,
        string CashboxName,
        string CashboxCode,
        DateOnly VoucherDate,
        CashDirection Direction,
        decimal Amount,
        CurrencyCode Currency,
        decimal ExchangeRate,
        decimal BaseOpeningBalance);

    private IQueryable<CashVoucherResponse> ProjectResponseQuery(int id) =>
        dbContext.CashVouchers
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.Id == id)
            .ProjectToType<CashVoucherResponse>();

    private async Task ApplyPreparationAsync(
        CashVoucher voucher,
        VoucherPreparation preparation,
        CancellationToken cancellationToken)
    {
        if (preparation.Cashbox is not null &&
            preparation.ExchangeRate is not null)
        {
            voucher.Currency = preparation.Cashbox.Currency;
            voucher.ApplyExchangeRate(
                preparation.ExchangeRate.ExchangeRateId,
                preparation.ExchangeRate.Rate);
            return;
        }

        voucher.CashboxId = null;
        voucher.CashMovementTypeId = null;
        voucher.AccountId = null;
        voucher.PartyType = CashPartyType.None;
        voucher.EmployeeId = null;
        voucher.BusinessPartnerId = null;
        voucher.DriverId = null;
        voucher.DriverTripId = null;
        voucher.ExternalPartyName = null;

        voucher.Currency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;
        voucher.ApplyExchangeRate(exchangeRateId: null, exchangeRate: 1m);
    }

    private sealed record VoucherPreparation(
        Cashbox? Cashbox,
        CashPartyType PartyType,
        BusinessPartner? BusinessPartner,
        Driver? Driver,
        ResolvedExchangeRate? ExchangeRate,
        CashMovementClassification? Classification);

    private static CashPartyType DerivePartyType(
        CashVoucherUpdateRequest request) =>
        request.EmployeeId.HasValue
            ? CashPartyType.Employee
            : request.BusinessPartnerId.HasValue
                ? CashPartyType.Partner
                : request.DriverId.HasValue
                    ? CashPartyType.Driver
                    : !string.IsNullOrWhiteSpace(request.ExternalPartyName)
                        ? CashPartyType.Other
                        : CashPartyType.None;

    private static bool HasAtMostOneTarget(CashVoucherUpdateRequest request)
    {
        var selectedPartyCount =
            (request.AccountId.HasValue ? 1 : 0) +
            (request.EmployeeId.HasValue ? 1 : 0) +
            (request.BusinessPartnerId.HasValue ? 1 : 0) +
            (request.DriverId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(request.ExternalPartyName) ? 1 : 0);

        return selectedPartyCount <= 1;
    }

    private static bool HasExactlyOnePostingTarget(
        CashVoucherUpdateRequest request)
    {
        var selectedPartyOrAccountCount =
            (request.AccountId.HasValue ? 1 : 0) +
            (request.EmployeeId.HasValue ? 1 : 0) +
            (request.BusinessPartnerId.HasValue ? 1 : 0) +
            (request.DriverId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(request.ExternalPartyName) ? 1 : 0);

        return selectedPartyOrAccountCount == 1 ||
            (selectedPartyOrAccountCount == 0 &&
             request.CashMovementTypeId.HasValue);
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
