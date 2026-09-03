using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using static MiniErp.Application.Features.Statements.StatementErrors;

namespace MiniErp.Infrastructure.Services.Statements;

public sealed partial class FinancialStatementService
{
    public async Task<Result<TrialBalanceResponse>> GetTrialBalanceAsync(
        TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                (!filters.FiscalYearId.HasValue ||
                 year.Id == filters.FiscalYearId.Value) &&
                year.StartDate <= filters.FromDate &&
                year.EndDate >= filters.ToDate)
            .OrderByDescending(year => year.IsCurrent)
            .ThenBy(year => year.StartDate)
            .Select(year => new
            {
                year.Id,
                year.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (fiscalYear is null)
        {
            return Result<TrialBalanceResponse>.Failure(
                FiscalYearNotFound(filters.FiscalYearId));
        }

        var accountRows = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.CompanyId == companyId &&
                account.IsActive &&
                account.IsPosting)
            .Select(account => new TrialBalanceAccountRow
            {
                Id = account.Id,
                Code = account.Code,
                Name = account.Name,
                AccountType = account.AccountType
            })
            .ToListAsync(cancellationToken);

        var ledger = new TrialBalanceLedger(
            accountRows,
            filters.FromDate);
        await LoadJournalEntriesAsync(
            ledger,
            fiscalYear.Id,
            filters,
            cancellationToken);

        var items = ledger.ToItems(
            filters.IncludeZeroBalances,
            filters.IncludeUnclassified,
            filters.ViewMode);
        var totals = new TrialBalanceTotalsResponse(
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

        return Result<TrialBalanceResponse>.Success(
            new TrialBalanceResponse(
                FiscalYearId: fiscalYear.Id,
                FiscalYearName: fiscalYear.Name,
                FromDate: filters.FromDate,
                ToDate: filters.ToDate,
                BaseCurrency: baseCurrency,
                ViewMode: filters.ViewMode,
                AdjustmentView: filters.AdjustmentView,
                IsOperationalOnly: false,
                Items: items,
                Totals: totals));
    }

    private async Task LoadJournalEntriesAsync(
        TrialBalanceLedger ledger,
        int fiscalYearId,
        TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var includeAdjustments = filters.AdjustmentView ==
            TrialBalanceAdjustmentView.AfterAdjustments;
        var lines = await dbContext.JournalEntryLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.JournalEntry.FiscalYearId == fiscalYearId &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.ReversalOfEntryId == null &&
                line.JournalEntry.EntryDate <= filters.ToDate &&
                (includeAdjustments ||
                 line.JournalEntry.EntryType != JournalEntryType.Adjustment))
            .Select(line => new
            {
                line.JournalEntry.EntryDate,
                line.AccountId,
                line.Debit,
                line.Credit
            })
            .ToListAsync(cancellationToken);

        foreach (var line in lines)
        {
            ledger.Add(
                line.AccountId,
                line.EntryDate,
                line.Debit,
                line.Credit);
        }
    }

    private sealed class TrialBalanceLedger
    {
        private readonly DateOnly fromDate;
        private readonly Dictionary<int, TrialBalanceAccountBalance> accounts;
        private readonly TrialBalanceAccountBalance unclassified = new(
            accountId: null,
            accountCode: null,
            accountName: "غير مصنف",
            accountType: null,
            isUnclassified: true);

        public TrialBalanceLedger(
            IReadOnlyCollection<TrialBalanceAccountRow> accountRows,
            DateOnly fromDate)
        {
            this.fromDate = fromDate;
            accounts = accountRows.ToDictionary(
                row => row.Id,
                row => new TrialBalanceAccountBalance(
                    accountId: row.Id,
                    accountCode: row.Code,
                    accountName: row.Name,
                    accountType: row.AccountType,
                    isUnclassified: false));
        }

        public void Add(
            int accountId,
            DateOnly date,
            decimal debit,
            decimal credit)
        {
            var account = accounts.TryGetValue(accountId, out var value)
                ? value
                : unclassified;
            if (date < fromDate)
            {
                account.OpeningDebit += debit;
                account.OpeningCredit += credit;
                return;
            }

            account.PeriodDebit += debit;
            account.PeriodCredit += credit;
        }

        public IReadOnlyList<TrialBalanceItemResponse> ToItems(
            bool includeZeroBalances,
            bool includeUnclassified,
            TrialBalanceViewMode viewMode)
        {
            var rows = accounts.Values
                .Append(unclassified)
                .Where(row => includeZeroBalances || !row.IsZero)
                .Where(row => includeUnclassified || !row.IsUnclassified)
                .Select(ToItem)
                .OrderBy(row => row.IsUnclassified)
                .ThenBy(row => row.AccountCode)
                .ThenBy(row => row.AccountName)
                .ToArray();

            if (viewMode != TrialBalanceViewMode.Summary)
            {
                return rows;
            }

            return rows
                .GroupBy(row => new
                {
                    row.IsUnclassified,
                    row.AccountType
                })
                .Select(group => new TrialBalanceItemResponse(
                    AccountId: null,
                    AccountCode: null,
                    AccountName: group.Key.IsUnclassified
                        ? "غير مصنف"
                        : AccountTypeName(group.Key.AccountType!.Value),
                    AccountType: group.Key.AccountType,
                    IsUnclassified: group.Key.IsUnclassified,
                    OpeningDebit: group.Sum(row => row.OpeningDebit),
                    OpeningCredit: group.Sum(row => row.OpeningCredit),
                    PeriodDebit: group.Sum(row => row.PeriodDebit),
                    PeriodCredit: group.Sum(row => row.PeriodCredit),
                    ClosingDebit: group.Sum(row => row.ClosingDebit),
                    ClosingCredit: group.Sum(row => row.ClosingCredit)))
                .OrderBy(row => row.IsUnclassified)
                .ThenBy(row => row.AccountType)
                .ToArray();
        }

        private static TrialBalanceItemResponse ToItem(
            TrialBalanceAccountBalance row)
        {
            var openingSigned = row.OpeningDebit - row.OpeningCredit;
            var closingSigned = openingSigned +
                row.PeriodDebit -
                row.PeriodCredit;
            return new TrialBalanceItemResponse(
                AccountId: row.AccountId,
                AccountCode: row.AccountCode,
                AccountName: row.AccountName,
                AccountType: row.AccountType,
                IsUnclassified: row.IsUnclassified,
                OpeningDebit: row.OpeningDebit,
                OpeningCredit: row.OpeningCredit,
                PeriodDebit: row.PeriodDebit,
                PeriodCredit: row.PeriodCredit,
                ClosingDebit: Math.Max(closingSigned, 0m),
                ClosingCredit: Math.Max(-closingSigned, 0m));
        }

        private static string AccountTypeName(AccountType type) => type switch
        {
            AccountType.Asset => "الأصول",
            AccountType.Liability => "الالتزامات",
            AccountType.Equity => "حقوق الملكية",
            AccountType.Revenue => "الإيرادات",
            AccountType.Expense => "المصروفات",
            _ => type.ToString()
        };
    }

    private sealed class TrialBalanceAccountRow
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public AccountType AccountType { get; init; }
    }

    private sealed class TrialBalanceAccountBalance(
        int? accountId,
        string? accountCode,
        string accountName,
        AccountType? accountType,
        bool isUnclassified)
    {
        public int? AccountId { get; } = accountId;
        public string? AccountCode { get; } = accountCode;
        public string AccountName { get; } = accountName;
        public AccountType? AccountType { get; } = accountType;
        public bool IsUnclassified { get; } = isUnclassified;
        public decimal OpeningDebit { get; set; }
        public decimal OpeningCredit { get; set; }
        public decimal PeriodDebit { get; set; }
        public decimal PeriodCredit { get; set; }

        public bool IsZero =>
            OpeningDebit == 0m &&
            OpeningCredit == 0m &&
            PeriodDebit == 0m &&
            PeriodCredit == 0m;
    }
}
