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
        if (ShouldLoadCategory(filters, OperationalTrialBalanceCategory.Cashbox))
        {
            balances.AddRange(await LoadPartyBalancesAsync(
                filters,
                JournalPartyType.Cashbox,
                OperationalTrialBalanceCategory.Cashbox,
                cancellationToken));
        }
        if (ShouldLoadCategory(filters, OperationalTrialBalanceCategory.Partner))
        {
            balances.AddRange(await LoadPartyBalancesAsync(
                filters,
                JournalPartyType.Customer,
                OperationalTrialBalanceCategory.Partner,
                cancellationToken));
        }
        if (ShouldLoadCategory(filters, OperationalTrialBalanceCategory.Driver))
        {
            balances.AddRange(await LoadPartyBalancesAsync(
                filters,
                JournalPartyType.Driver,
                OperationalTrialBalanceCategory.Driver,
                cancellationToken));
        }
        if (ShouldLoadCategory(filters, OperationalTrialBalanceCategory.Employee))
        {
            balances.AddRange(await LoadPartyBalancesAsync(
                filters,
                JournalPartyType.Employee,
                OperationalTrialBalanceCategory.Employee,
                cancellationToken));
        }
        if (ShouldLoadCategory(filters, OperationalTrialBalanceCategory.Revenue))
        {
            balances.AddRange(await LoadAccountBalancesAsync(
                filters,
                AccountType.Revenue,
                OperationalTrialBalanceCategory.Revenue,
                cancellationToken));
        }
        if (ShouldLoadCategory(filters, OperationalTrialBalanceCategory.Expense))
        {
            balances.AddRange(await LoadAccountBalancesAsync(
                filters,
                AccountType.Expense,
                OperationalTrialBalanceCategory.Expense,
                cancellationToken));
        }

        IReadOnlyList<OperationalTrialBalanceItemResponse> items = balances
            .Select(ToDetailedItem)
            .Where(item => filters.IncludeZeroBalances || !IsZero(item))
            .OrderBy(item => item.Category)
            .ThenBy(item => item.AccountCode)
            .ThenBy(item => item.AccountName)
            .ToArray();

        if (filters.ViewMode == OperationalTrialBalanceViewMode.Summary)
        {
            items = items
                .GroupBy(item => new { item.Category, item.CategoryName })
                .Select(group => new OperationalTrialBalanceItemResponse(
                    Category: group.Key.Category,
                    CategoryName: group.Key.CategoryName,
                    AccountId: null,
                    AccountCode: null,
                    AccountName: group.Key.CategoryName,
                    OpeningDebit: group.Sum(item => item.OpeningDebit),
                    OpeningCredit: group.Sum(item => item.OpeningCredit),
                    PeriodDebit: group.Sum(item => item.PeriodDebit),
                    PeriodCredit: group.Sum(item => item.PeriodCredit),
                    ClosingDebit: group.Sum(item => item.ClosingDebit),
                    ClosingCredit: group.Sum(item => item.ClosingCredit)))
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
        LoadPartyBalancesAsync(
            OperationalTrialBalanceFilterRequest filters,
            JournalPartyType partyType,
            OperationalTrialBalanceCategory category,
            CancellationToken cancellationToken)
    {
        var parties = partyType switch
        {
            JournalPartyType.Cashbox => await dbContext.Cashboxes
                .AsNoTracking()
                .Where(party => party.CompanyId == companyId)
                .Select(party => new PartyAccountProjection
                {
                    Id = party.Id,
                    Code = party.Code,
                    Name = party.Name
                })
                .ToListAsync(cancellationToken),
            JournalPartyType.Customer or JournalPartyType.Supplier =>
                await dbContext.BusinessPartners
                    .AsNoTracking()
                    .Where(party => party.CompanyId == companyId)
                    .Select(party => new PartyAccountProjection
                    {
                        Id = party.Id,
                        Code = party.Code,
                        Name = party.Name
                    })
                    .ToListAsync(cancellationToken),
            JournalPartyType.Employee => await dbContext.Employees
                .AsNoTracking()
                .Where(party => party.CompanyId == companyId)
                .Select(party => new PartyAccountProjection
                {
                    Id = party.Id,
                    Code = party.Code,
                    Name = party.Name
                })
                .ToListAsync(cancellationToken),
            JournalPartyType.Driver => await dbContext.Drivers
                .AsNoTracking()
                .Where(party => party.CompanyId == companyId)
                .Select(party => new PartyAccountProjection
                {
                    Id = party.Id,
                    Code = party.Code,
                    Name = party.Name
                })
                .ToListAsync(cancellationToken),
            _ => []
        };

        var accounts = parties.ToDictionary(
            party => party.Id,
            party => new OperationalAccountBalance(
                category: category,
                categoryName: CategoryName(category),
                accountId: party.Id,
                accountCode: party.Code,
                accountName: party.Name));
        var partyTypes = category == OperationalTrialBalanceCategory.Partner
            ? new[] { JournalPartyType.Customer, JournalPartyType.Supplier }
            : new[] { partyType };
        var groups = await PostedLedgerLines()
            .Where(line =>
                line.PartyType.HasValue &&
                partyTypes.Contains(line.PartyType.Value) &&
                line.PartyId.HasValue &&
                line.JournalEntry.EntryDate <= filters.ToDate)
            .GroupBy(line => new
            {
                AccountId = line.PartyId!.Value,
                IsOpening = line.JournalEntry.EntryDate < filters.FromDate
            })
            .Select(group => new AccountMovementGroup(
                AccountId: group.Key.AccountId,
                IsOpening: group.Key.IsOpening,
                Debit: group.Sum(line => line.Debit),
                Credit: group.Sum(line => line.Credit)))
            .ToListAsync(cancellationToken);
        ApplyGroups(accounts, groups);
        return accounts.Values.ToArray();
    }

    private async Task<IReadOnlyList<OperationalAccountBalance>>
        LoadAccountBalancesAsync(
            OperationalTrialBalanceFilterRequest filters,
            AccountType accountType,
            OperationalTrialBalanceCategory category,
            CancellationToken cancellationToken)
    {
        var accounts = (await dbContext.Accounts
                .AsNoTracking()
                .Where(account =>
                    account.CompanyId == companyId &&
                    account.AccountType == accountType)
                .Select(account => new
                {
                    account.Id,
                    account.Code,
                    account.Name
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(
                account => account.Id,
                account => new OperationalAccountBalance(
                    category: category,
                    categoryName: CategoryName(category),
                    accountId: account.Id,
                    accountCode: account.Code,
                    accountName: account.Name));
        var groups = await PostedLedgerLines()
            .Where(line =>
                line.Account.CompanyId == companyId &&
                line.Account.AccountType == accountType &&
                line.JournalEntry.EntryDate <= filters.ToDate)
            .GroupBy(line => new
            {
                AccountId = line.AccountId,
                IsOpening = line.JournalEntry.EntryDate < filters.FromDate
            })
            .Select(group => new AccountMovementGroup(
                AccountId: group.Key.AccountId,
                IsOpening: group.Key.IsOpening,
                Debit: group.Sum(line => line.Debit),
                Credit: group.Sum(line => line.Credit)))
            .ToListAsync(cancellationToken);
        ApplyGroups(accounts, groups);
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

    private static OperationalTrialBalanceItemResponse ToDetailedItem(
        OperationalAccountBalance account)
    {
        var closingSigned = account.OpeningSigned +
            account.PeriodDebit - account.PeriodCredit;
        return new OperationalTrialBalanceItemResponse(
            Category: account.Category,
            CategoryName: account.CategoryName,
            AccountId: account.AccountId,
            AccountCode: account.AccountCode,
            AccountName: account.AccountName,
            OpeningDebit: Math.Max(account.OpeningSigned, 0m),
            OpeningCredit: Math.Max(-account.OpeningSigned, 0m),
            PeriodDebit: account.PeriodDebit,
            PeriodCredit: account.PeriodCredit,
            ClosingDebit: Math.Max(closingSigned, 0m),
            ClosingCredit: Math.Max(-closingSigned, 0m));
    }

    private static bool IsZero(OperationalTrialBalanceItemResponse item) =>
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
                nameof(category), category, null)
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

    private sealed class PartyAccountProjection
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }

    private sealed record AccountMovementGroup(
        int AccountId,
        bool IsOpening,
        decimal Debit,
        decimal Credit);
}
