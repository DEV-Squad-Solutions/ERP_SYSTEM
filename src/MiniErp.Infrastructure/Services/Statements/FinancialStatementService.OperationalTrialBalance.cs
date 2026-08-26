using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Statements;

public sealed partial class FinancialStatementService
{
    public async Task<Result<OperationalTrialBalanceResponse>>
        GetOperationalTrialBalanceAsync(
            OperationalTrialBalanceFilterRequest filters,
            CancellationToken cancellationToken = default)
    {
        var balances = new List<OperationalAccountBalance>();

        if (ShouldLoadCategory(
                filters,
                OperationalTrialBalanceCategory.Cashbox))
        {
            balances.AddRange(await LoadCashboxBalancesAsync(
                filters,
                cancellationToken));
        }

        if (ShouldLoadCategory(
                filters,
                OperationalTrialBalanceCategory.Partner))
        {
            balances.AddRange(await LoadPartnerBalancesAsync(
                filters,
                cancellationToken));
        }

        if (ShouldLoadCategory(
                filters,
                OperationalTrialBalanceCategory.Driver))
        {
            balances.AddRange(await LoadDriverBalancesAsync(
                filters,
                cancellationToken));
        }

        if (ShouldLoadCategory(
                filters,
                OperationalTrialBalanceCategory.Employee))
        {
            balances.AddRange(await LoadEmployeeBalancesAsync(
                filters,
                cancellationToken));
        }

        if (ShouldLoadCategory(
                filters,
                OperationalTrialBalanceCategory.Revenue))
        {
            balances.AddRange(await LoadMovementTypeBalancesAsync(
                filters,
                OperationalTrialBalanceCategory.Revenue,
                CashMovementClassification.Revenue,
                cancellationToken));
        }

        if (ShouldLoadCategory(
                filters,
                OperationalTrialBalanceCategory.Expense))
        {
            balances.AddRange(await LoadMovementTypeBalancesAsync(
                filters,
                OperationalTrialBalanceCategory.Expense,
                CashMovementClassification.Expense,
                cancellationToken));
        }

        IReadOnlyList<OperationalTrialBalanceItemResponse> items = balances
            .Select(ToDetailedItem)
            .Where(item =>
                filters.IncludeZeroBalances || !IsZero(item))
            .OrderBy(item => item.Category)
            .ThenBy(item => item.AccountCode)
            .ThenBy(item => item.AccountName)
            .ToArray();

        if (filters.ViewMode == OperationalTrialBalanceViewMode.Summary)
        {
            items = items
                .GroupBy(item => new
                {
                    item.Category,
                    item.CategoryName
                })
                .Select(group =>
                    new OperationalTrialBalanceItemResponse(
                        Category: group.Key.Category,
                        CategoryName: group.Key.CategoryName,
                        AccountId: null,
                        AccountCode: null,
                        AccountName: group.Key.CategoryName,
                        OpeningDebit: group.Sum(item =>
                            item.OpeningDebit),
                        OpeningCredit: group.Sum(item =>
                            item.OpeningCredit),
                        PeriodDebit: group.Sum(item =>
                            item.PeriodDebit),
                        PeriodCredit: group.Sum(item =>
                            item.PeriodCredit),
                        ClosingDebit: group.Sum(item =>
                            item.ClosingDebit),
                        ClosingCredit: group.Sum(item =>
                            item.ClosingCredit)))
                .OrderBy(item => item.Category)
                .ToArray();
        }

        var totals = new OperationalTrialBalanceTotalsResponse(
            OpeningDebit: items.Sum(item => item.OpeningDebit),
            OpeningCredit: items.Sum(item => item.OpeningCredit),
            PeriodDebit: items.Sum(item => item.PeriodDebit),
            PeriodCredit: items.Sum(item => item.PeriodCredit),
            ClosingDebit: items.Sum(item => item.ClosingDebit),
            ClosingCredit: items.Sum(item => item.ClosingCredit));

        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;

        return Result<OperationalTrialBalanceResponse>.Success(
            new OperationalTrialBalanceResponse(
                FromDate: filters.FromDate,
                ToDate: filters.ToDate,
                BaseCurrency: baseCurrency,
                ViewMode: filters.ViewMode,
                Items: items,
                Totals: totals));
    }

    private async Task<IReadOnlyList<OperationalAccountBalance>>
        LoadCashboxBalancesAsync(
            OperationalTrialBalanceFilterRequest filters,
            CancellationToken cancellationToken)
    {
        var accounts = (await dbContext.Cashboxes
                .AsNoTracking()
                .Where(cashbox => cashbox.CompanyId == companyId)
                .Select(cashbox => new
                {
                    cashbox.Id,
                    cashbox.Code,
                    cashbox.Name,
                    cashbox.OpeningBalanceDate,
                    cashbox.BaseOpeningBalance
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(
                cashbox => cashbox.Id,
                cashbox => new OperationalAccountBalance(
                    category: OperationalTrialBalanceCategory.Cashbox,
                    categoryName: CategoryName(
                        OperationalTrialBalanceCategory.Cashbox),
                    accountId: cashbox.Id,
                    accountCode: cashbox.Code,
                    accountName: cashbox.Name));

        foreach (var cashbox in await dbContext.Cashboxes
                     .AsNoTracking()
                     .Where(entity =>
                         entity.CompanyId == companyId &&
                         entity.OpeningBalanceDate <= filters.ToDate &&
                         entity.BaseOpeningBalance != 0m)
                     .Select(entity => new
                     {
                         entity.Id,
                         Date = entity.OpeningBalanceDate,
                         Amount = entity.BaseOpeningBalance
                     })
                     .ToListAsync(cancellationToken))
        {
            ApplySignedAmount(
                accounts[cashbox.Id],
                cashbox.Date,
                cashbox.Amount,
                filters.FromDate);
        }

        var voucherGroups = await dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.IsPosted &&
                voucher.CashboxId.HasValue &&
                voucher.VoucherDate <= filters.ToDate)
            .GroupBy(voucher => new
            {
                AccountId = voucher.CashboxId!.Value,
                IsOpening = voucher.VoucherDate < filters.FromDate
            })
            .Select(group => new
            {
                group.Key.AccountId,
                group.Key.IsOpening,
                Debit = group.Sum(voucher =>
                    voucher.Direction == CashDirection.Receipt
                        ? voucher.BaseAmount
                        : 0m),
                Credit = group.Sum(voucher =>
                    voucher.Direction == CashDirection.Payment
                        ? voucher.BaseAmount
                        : 0m)
            })
            .ToListAsync(cancellationToken);

        ApplyGroups(accounts, voucherGroups.Select(group =>
            new AccountMovementGroup(
                AccountId: group.AccountId,
                IsOpening: group.IsOpening,
                Debit: group.Debit,
                Credit: group.Credit)));

        return accounts.Values.ToArray();
    }

    private async Task<IReadOnlyList<OperationalAccountBalance>>
        LoadPartnerBalancesAsync(
            OperationalTrialBalanceFilterRequest filters,
            CancellationToken cancellationToken)
    {
        var accounts = (await dbContext.BusinessPartners
                .AsNoTracking()
                .Where(partner => partner.CompanyId == companyId)
                .Select(partner => new
                {
                    partner.Id,
                    partner.Code,
                    partner.Name
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(
                partner => partner.Id,
                partner => new OperationalAccountBalance(
                    category: OperationalTrialBalanceCategory.Partner,
                    categoryName: CategoryName(
                        OperationalTrialBalanceCategory.Partner),
                    accountId: partner.Id,
                    accountCode: partner.Code,
                    accountName: partner.Name));

        var openingGroups = await dbContext.PartnerOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.DocumentDate <= filters.ToDate)
            .GroupBy(balance => new
            {
                AccountId = balance.BusinessPartnerId,
                IsOpening = balance.DocumentDate < filters.FromDate
            })
            .Select(group => new
            {
                group.Key.AccountId,
                group.Key.IsOpening,
                Debit = group.Sum(balance =>
                    balance.BalanceType == PartnerBalanceType.Receivable
                        ? balance.BaseAmount
                        : 0m),
                Credit = group.Sum(balance =>
                    balance.BalanceType == PartnerBalanceType.Payable
                        ? balance.BaseAmount
                        : 0m)
            })
            .ToListAsync(cancellationToken);

        ApplyGroups(accounts, openingGroups.Select(group =>
            new AccountMovementGroup(
                AccountId: group.AccountId,
                IsOpening: group.IsOpening,
                Debit: group.Debit,
                Credit: group.Credit)));

        var movementGroups = await dbContext.BusinessPartnerMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.MovementDate <= filters.ToDate)
            .GroupBy(movement => new
            {
                AccountId = movement.BusinessPartnerId,
                IsOpening = movement.MovementDate < filters.FromDate
            })
            .Select(group => new
            {
                group.Key.AccountId,
                group.Key.IsOpening,
                Debit = group.Sum(movement => movement.BaseDebit),
                Credit = group.Sum(movement => movement.BaseCredit)
            })
            .ToListAsync(cancellationToken);

        ApplyGroups(accounts, movementGroups.Select(group =>
            new AccountMovementGroup(
                AccountId: group.AccountId,
                IsOpening: group.IsOpening,
                Debit: group.Debit,
                Credit: group.Credit)));

        return accounts.Values.ToArray();
    }

    private async Task<IReadOnlyList<OperationalAccountBalance>>
        LoadDriverBalancesAsync(
            OperationalTrialBalanceFilterRequest filters,
            CancellationToken cancellationToken)
    {
        var accounts = (await dbContext.Drivers
                .AsNoTracking()
                .Where(driver => driver.CompanyId == companyId)
                .Select(driver => new
                {
                    driver.Id,
                    driver.Code,
                    driver.Name
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(
                driver => driver.Id,
                driver => new OperationalAccountBalance(
                    category: OperationalTrialBalanceCategory.Driver,
                    categoryName: CategoryName(
                        OperationalTrialBalanceCategory.Driver),
                    accountId: driver.Id,
                    accountCode: driver.Code,
                    accountName: driver.Name));

        var voucherGroups = await dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.IsPosted &&
                voucher.DriverId.HasValue &&
                voucher.VoucherDate <= filters.ToDate)
            .GroupBy(voucher => new
            {
                AccountId = voucher.DriverId!.Value,
                IsOpening = voucher.VoucherDate < filters.FromDate
            })
            .Select(group => new
            {
                group.Key.AccountId,
                group.Key.IsOpening,
                Debit = group.Sum(voucher =>
                    voucher.Direction == CashDirection.Payment
                        ? voucher.BaseAmount
                        : 0m),
                Credit = group.Sum(voucher =>
                    voucher.Direction == CashDirection.Receipt
                        ? voucher.BaseAmount
                        : 0m)
            })
            .ToListAsync(cancellationToken);

        ApplyGroups(accounts, voucherGroups.Select(group =>
            new AccountMovementGroup(
                AccountId: group.AccountId,
                IsOpening: group.IsOpening,
                Debit: group.Debit,
                Credit: group.Credit)));

        var tripGroups = await dbContext.DriverTrips
            .AsNoTracking()
            .Where(trip =>
                trip.CompanyId == companyId &&
                trip.Cost.HasValue &&
                trip.TripDate <= filters.ToDate)
            .GroupBy(trip => new
            {
                AccountId = trip.DriverId,
                IsOpening = trip.TripDate < filters.FromDate
            })
            .Select(group => new
            {
                group.Key.AccountId,
                group.Key.IsOpening,
                Credit = group.Sum(trip => trip.Cost ?? 0m)
            })
            .ToListAsync(cancellationToken);

        ApplyGroups(accounts, tripGroups.Select(group =>
            new AccountMovementGroup(
                AccountId: group.AccountId,
                IsOpening: group.IsOpening,
                Debit: 0m,
                Credit: group.Credit)));

        return accounts.Values.ToArray();
    }

    private async Task<IReadOnlyList<OperationalAccountBalance>>
        LoadEmployeeBalancesAsync(
            OperationalTrialBalanceFilterRequest filters,
            CancellationToken cancellationToken)
    {
        var accounts = (await dbContext.Employees
                .AsNoTracking()
                .Where(employee => employee.CompanyId == companyId)
                .Select(employee => new
                {
                    employee.Id,
                    employee.Code,
                    employee.Name
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(
                employee => employee.Id,
                employee => new OperationalAccountBalance(
                    category: OperationalTrialBalanceCategory.Employee,
                    categoryName: CategoryName(
                        OperationalTrialBalanceCategory.Employee),
                    accountId: employee.Id,
                    accountCode: employee.Code,
                    accountName: employee.Name));

        var transactionGroups = await dbContext.EmployeeTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.CompanyId == companyId &&
                transaction.TransactionDate <= filters.ToDate)
            .GroupBy(transaction => new
            {
                AccountId = transaction.EmployeeId,
                IsOpening = transaction.TransactionDate < filters.FromDate
            })
            .Select(group => new
            {
                group.Key.AccountId,
                group.Key.IsOpening,
                Debit = group.Sum(transaction =>
                    transaction.Type == EmployeeTransactionType.Credit ||
                    transaction.Type == EmployeeTransactionType.Bonus
                        ? 0m
                        : transaction.Amount),
                Credit = group.Sum(transaction =>
                    transaction.Type == EmployeeTransactionType.Credit ||
                    transaction.Type == EmployeeTransactionType.Bonus
                        ? transaction.Amount
                        : 0m)
            })
            .ToListAsync(cancellationToken);

        ApplyGroups(accounts, transactionGroups.Select(group =>
            new AccountMovementGroup(
                AccountId: group.AccountId,
                IsOpening: group.IsOpening,
                Debit: group.Debit,
                Credit: group.Credit)));

        var directVoucherGroups = await dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.IsPosted &&
                voucher.EmployeeId.HasValue &&
                voucher.VoucherDate <= filters.ToDate &&
                !dbContext.EmployeeTransactions.Any(transaction =>
                    transaction.CompanyId == companyId &&
                    transaction.CashVoucherId == voucher.Id))
            .GroupBy(voucher => new
            {
                AccountId = voucher.EmployeeId!.Value,
                IsOpening = voucher.VoucherDate < filters.FromDate
            })
            .Select(group => new
            {
                group.Key.AccountId,
                group.Key.IsOpening,
                Debit = group.Sum(voucher =>
                    voucher.Direction == CashDirection.Payment
                        ? voucher.BaseAmount
                        : 0m),
                Credit = group.Sum(voucher =>
                    voucher.Direction == CashDirection.Receipt
                        ? voucher.BaseAmount
                        : 0m)
            })
            .ToListAsync(cancellationToken);

        ApplyGroups(accounts, directVoucherGroups.Select(group =>
            new AccountMovementGroup(
                AccountId: group.AccountId,
                IsOpening: group.IsOpening,
                Debit: group.Debit,
                Credit: group.Credit)));

        return accounts.Values.ToArray();
    }

    private async Task<IReadOnlyList<OperationalAccountBalance>>
        LoadMovementTypeBalancesAsync(
            OperationalTrialBalanceFilterRequest filters,
            OperationalTrialBalanceCategory category,
            CashMovementClassification classification,
            CancellationToken cancellationToken)
    {
        var accounts = (await dbContext.CashMovementTypes
                .AsNoTracking()
                .Where(movementType =>
                    movementType.CompanyId == companyId &&
                    movementType.Classification == classification)
                .Select(movementType => new
                {
                    movementType.Id,
                    movementType.Name
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(
                movementType => movementType.Id,
                movementType => new OperationalAccountBalance(
                    category: category,
                    categoryName: CategoryName(category),
                    accountId: movementType.Id,
                    accountCode: null,
                    accountName: movementType.Name));

        var voucherGroups = await dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.IsPosted &&
                voucher.CashMovementTypeId.HasValue &&
                voucher.CashMovementType != null &&
                voucher.CashMovementType.Classification == classification &&
                voucher.VoucherDate <= filters.ToDate)
            .GroupBy(voucher => new
            {
                AccountId = voucher.CashMovementTypeId!.Value,
                IsOpening = voucher.VoucherDate < filters.FromDate
            })
            .Select(group => new
            {
                group.Key.AccountId,
                group.Key.IsOpening,
                Debit = group.Sum(voucher =>
                    voucher.Direction == CashDirection.Payment
                        ? voucher.BaseAmount
                        : 0m),
                Credit = group.Sum(voucher =>
                    voucher.Direction == CashDirection.Receipt
                        ? voucher.BaseAmount
                        : 0m)
            })
            .ToListAsync(cancellationToken);

        ApplyGroups(accounts, voucherGroups.Select(group =>
            new AccountMovementGroup(
                AccountId: group.AccountId,
                IsOpening: group.IsOpening,
                Debit: group.Debit,
                Credit: group.Credit)));

        return accounts.Values.ToArray();
    }

    private static bool ShouldLoadCategory(
        OperationalTrialBalanceFilterRequest filters,
        OperationalTrialBalanceCategory category) =>
        !filters.Category.HasValue || filters.Category.Value == category;

    private static void ApplyGroups(
        IReadOnlyDictionary<int, OperationalAccountBalance> accounts,
        IEnumerable<AccountMovementGroup> groups)
    {
        foreach (var group in groups)
        {
            if (!accounts.TryGetValue(group.AccountId, out var account))
            {
                continue;
            }

            if (group.IsOpening)
            {
                account.OpeningSigned += group.Debit - group.Credit;
            }
            else
            {
                account.PeriodDebit += group.Debit;
                account.PeriodCredit += group.Credit;
            }
        }
    }

    private static void ApplySignedAmount(
        OperationalAccountBalance account,
        DateOnly date,
        decimal signedAmount,
        DateOnly fromDate)
    {
        if (date < fromDate)
        {
            account.OpeningSigned += signedAmount;
        }
        else if (signedAmount >= 0m)
        {
            account.PeriodDebit += signedAmount;
        }
        else
        {
            account.PeriodCredit += -signedAmount;
        }
    }

    private static OperationalTrialBalanceItemResponse ToDetailedItem(
        OperationalAccountBalance account)
    {
        var openingDebit = Math.Max(account.OpeningSigned, 0m);
        var openingCredit = Math.Max(-account.OpeningSigned, 0m);
        var closingSigned = account.OpeningSigned +
            account.PeriodDebit - account.PeriodCredit;

        return new OperationalTrialBalanceItemResponse(
            Category: account.Category,
            CategoryName: account.CategoryName,
            AccountId: account.AccountId,
            AccountCode: account.AccountCode,
            AccountName: account.AccountName,
            OpeningDebit: openingDebit,
            OpeningCredit: openingCredit,
            PeriodDebit: account.PeriodDebit,
            PeriodCredit: account.PeriodCredit,
            ClosingDebit: Math.Max(closingSigned, 0m),
            ClosingCredit: Math.Max(-closingSigned, 0m));
    }

    private static bool IsZero(
        OperationalTrialBalanceItemResponse item) =>
        item.OpeningDebit == 0m &&
        item.OpeningCredit == 0m &&
        item.PeriodDebit == 0m &&
        item.PeriodCredit == 0m &&
        item.ClosingDebit == 0m &&
        item.ClosingCredit == 0m;

    private static string CategoryName(
        OperationalTrialBalanceCategory category) => category switch
        {
            OperationalTrialBalanceCategory.Cashbox => "الخزائن",
            OperationalTrialBalanceCategory.Partner => "العملاء والموردون",
            OperationalTrialBalanceCategory.Driver => "السائقون",
            OperationalTrialBalanceCategory.Employee => "الموظفون",
            OperationalTrialBalanceCategory.Revenue => "الإيرادات",
            OperationalTrialBalanceCategory.Expense => "المصروفات",
            _ => throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                null)
        };

    private sealed class OperationalAccountBalance(
        OperationalTrialBalanceCategory category,
        string categoryName,
        int accountId,
        string? accountCode,
        string accountName)
    {
        public OperationalTrialBalanceCategory Category { get; } = category;

        public string CategoryName { get; } = categoryName;

        public int AccountId { get; } = accountId;

        public string? AccountCode { get; } = accountCode;

        public string AccountName { get; } = accountName;

        public decimal OpeningSigned { get; set; }

        public decimal PeriodDebit { get; set; }

        public decimal PeriodCredit { get; set; }
    }

    private sealed record AccountMovementGroup(
        int AccountId,
        bool IsOpening,
        decimal Debit,
        decimal Credit);
}
