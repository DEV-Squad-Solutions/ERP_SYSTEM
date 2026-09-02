using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using static MiniErp.Application.Features.Statements.StatementErrors;

namespace MiniErp.Infrastructure.Services.Statements;

public sealed partial class FinancialStatementService
{
    public async Task<Result<FinancialStatementReportResponse>>
        GetFinancialStatementReportAsync(
            FinancialStatementType statementType,
            FinancialStatementReportRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(statementType))
        {
            return Result<FinancialStatementReportResponse>.Failure(
                InvalidStatementType(statementType));
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                (!request.FiscalYearId.HasValue ||
                 year.Id == request.FiscalYearId.Value) &&
                year.StartDate <= request.FromDate &&
                year.EndDate >= request.ToDate)
            .OrderByDescending(year => year.IsCurrent)
            .ThenBy(year => year.StartDate)
            .Select(year => new
            {
                year.Id,
                year.Name,
                year.StartDate,
                year.EndDate
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (fiscalYear is null)
        {
            return Result<FinancialStatementReportResponse>.Failure(
                FiscalYearNotFound(request.FiscalYearId));
        }

        var includeAdjustments = request.AdjustmentView ==
            TrialBalanceAdjustmentView.AfterAdjustments;
        var journalLines = await LoadReportJournalLinesAsync(
            fiscalYear.Id,
            request,
            includeAdjustments,
            cancellationToken);
        var cashAccountIds = statementType == FinancialStatementType.CashFlow
            ? (await dbContext.AccountMappings
                .AsNoTracking()
                .Where(mapping =>
                    mapping.CompanyId == companyId &&
                    mapping.FiscalYearId == fiscalYear.Id &&
                    mapping.MappingType == AccountingMappingType.Cashbox)
                .Select(mapping => mapping.AccountId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet()
            : new HashSet<int>();
        var mappings = await dbContext.AccountStatementMappings
            .AsNoTracking()
            .Where(mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYear.Id &&
                mapping.StatementType == statementType)
            .Select(mapping => new ReportMappingRow
            {
                AccountId = mapping.AccountId,
                FinancialStatementLineId = mapping.FinancialStatementLineId,
                FinancialStatementLineCode = mapping.FinancialStatementLine.Code,
                FinancialStatementLineName = mapping.FinancialStatementLine.Name
            })
            .ToListAsync(cancellationToken);

        var lines = statementType == FinancialStatementType.CashFlow
            ? BuildCashFlowItems(
                journalLines,
                request,
                mappings,
                cashAccountIds)
            : BuildAccountStatementItems(
                journalLines,
                statementType,
                request,
                mappings);
        var unmappedAccounts = BuildUnmappedAccounts(
            journalLines,
            statementType,
            request,
            mappings,
            cashAccountIds);
        var items = request.ViewMode == TrialBalanceViewMode.Summary
            ? SummarizeItems(lines)
            : lines
                .OrderBy(item => item.FinancialStatementLineCode)
                .ThenBy(item => item.AccountCode)
                .ToArray();
        var totals = BuildReportTotals(
            statementType,
            lines,
            journalLines,
            request,
            unmappedAccounts);

        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;

        return Result<FinancialStatementReportResponse>.Success(
            new FinancialStatementReportResponse(
                StatementType: statementType,
                FiscalYearId: fiscalYear.Id,
                FiscalYearName: fiscalYear.Name,
                FromDate: request.FromDate,
                ToDate: request.ToDate,
                BaseCurrency: baseCurrency,
                ViewMode: request.ViewMode,
                AdjustmentView: request.AdjustmentView,
                IsReadyForReporting: unmappedAccounts.Count == 0,
                Items: items,
                Totals: totals,
                UnmappedAccounts: request.IncludeUnmapped
                    ? unmappedAccounts
                    : []));
    }

    private async Task<List<ReportJournalLine>> LoadReportJournalLinesAsync(
        int fiscalYearId,
        FinancialStatementReportRequest request,
        bool includeAdjustments,
        CancellationToken cancellationToken)
    {
        return await dbContext.JournalEntryLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.JournalEntry.FiscalYearId == fiscalYearId &&
                line.JournalEntry.EntryDate <= request.ToDate &&
                (includeAdjustments ||
                 line.JournalEntry.EntryType != JournalEntryType.Adjustment))
            .Select(line => new ReportJournalLine
            {
                JournalEntryId = line.JournalEntryId,
                EntryDate = line.JournalEntry.EntryDate,
                AccountId = line.AccountId,
                AccountCode = line.Account.Code,
                AccountName = line.Account.Name,
                AccountType = line.Account.AccountType,
                Debit = line.Debit,
                Credit = line.Credit
            })
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyList<FinancialStatementReportItemResponse>
        BuildAccountStatementItems(
            IReadOnlyList<ReportJournalLine> journalLines,
            FinancialStatementType statementType,
            FinancialStatementReportRequest request,
            IReadOnlyList<ReportMappingRow> mappings)
    {
        var validMappings = mappings
            .GroupBy(mapping => mapping.AccountId)
            .ToDictionary(group => group.Key, group => group.First());
        var accounts = journalLines
            .GroupBy(line => line.AccountId)
            .ToDictionary(group => group.Key, group => group.First());
        var buckets = new Dictionary<(int LineId, int AccountId), ReportAmount>();

        foreach (var line in journalLines)
        {
            if (!IsAccountInStatement(line.AccountType, statementType) ||
                !validMappings.TryGetValue(line.AccountId, out var mapping) ||
                (line.EntryDate < request.FromDate &&
                 statementType != FinancialStatementType.FinancialPosition))
            {
                continue;
            }

            var bucket = buckets.GetValueOrDefault(
                (mapping.FinancialStatementLineId, line.AccountId)) ??
                new ReportAmount();
            if (statementType == FinancialStatementType.FinancialPosition &&
                line.EntryDate < request.FromDate)
            {
                bucket.OpeningDebit += line.Debit;
                bucket.OpeningCredit += line.Credit;
            }
            else
            {
                bucket.PeriodDebit += line.Debit;
                bucket.PeriodCredit += line.Credit;
            }

            buckets[(mapping.FinancialStatementLineId, line.AccountId)] = bucket;
        }

        return buckets
            .Select(pair =>
            {
                var mapping = validMappings[pair.Key.AccountId];
                return ToReportItem(
                    mapping,
                    pair.Key.AccountId,
                    accounts[pair.Key.AccountId],
                    pair.Value);
            })
            .ToArray();
    }

    private static IReadOnlyList<FinancialStatementReportItemResponse>
        BuildCashFlowItems(
            IReadOnlyList<ReportJournalLine> journalLines,
            FinancialStatementReportRequest request,
            IReadOnlyList<ReportMappingRow> mappings,
            IReadOnlySet<int> cashAccountIds)
    {
        var mappingByAccount = mappings
            .GroupBy(mapping => mapping.AccountId)
            .ToDictionary(group => group.Key, group => group.First());
        var accountLookup = journalLines
            .GroupBy(line => line.AccountId)
            .ToDictionary(group => group.Key, group => group.First());
        var buckets = new Dictionary<(int LineId, int AccountId), ReportAmount>();

        foreach (var entry in journalLines
                     .Where(line => line.EntryDate >= request.FromDate)
                     .GroupBy(line => line.JournalEntryId))
        {
            var cashEffect = entry
                .Where(line => cashAccountIds.Contains(line.AccountId))
                .Sum(line => line.Debit - line.Credit);
            if (cashEffect == 0m)
            {
                continue;
            }

            var counterparts = entry
                .Where(line => !cashAccountIds.Contains(line.AccountId))
                .ToArray();
            var totalWeight = counterparts.Sum(line =>
                Math.Abs(line.Debit - line.Credit));
            if (totalWeight == 0m)
            {
                continue;
            }

            foreach (var counterpart in counterparts)
            {
                var weight = Math.Abs(
                    counterpart.Debit - counterpart.Credit) /
                    totalWeight;
                var effect = decimal.Round(
                    cashEffect * weight,
                    8,
                    MidpointRounding.AwayFromZero);
                if (effect == 0m ||
                    !mappingByAccount.TryGetValue(
                        counterpart.AccountId,
                        out var mapping))
                {
                    continue;
                }

                var bucket = buckets.GetValueOrDefault(
                    (mapping.FinancialStatementLineId, counterpart.AccountId)) ??
                    new ReportAmount();
                if (effect >= 0m)
                {
                    bucket.PeriodDebit += effect;
                }
                else
                {
                    bucket.PeriodCredit += -effect;
                }

                buckets[(mapping.FinancialStatementLineId, counterpart.AccountId)] =
                    bucket;
            }
        }

        return buckets
            .Select(pair =>
            {
                var mapping = mappingByAccount[pair.Key.AccountId];
                return ToReportItem(
                    mapping,
                    pair.Key.AccountId,
                    accountLookup[pair.Key.AccountId],
                    pair.Value);
            })
            .ToArray();
    }

    private static IReadOnlyList<FinancialStatementUnmappedAccountResponse>
        BuildUnmappedAccounts(
            IReadOnlyList<ReportJournalLine> journalLines,
            FinancialStatementType statementType,
            FinancialStatementReportRequest request,
            IReadOnlyList<ReportMappingRow> mappings,
            IReadOnlySet<int>? ignoredAccountIds = null)
    {
        var mappedAccountIds = mappings
            .Select(mapping => mapping.AccountId)
            .ToHashSet();
        var amounts = new Dictionary<int, ReportAmount>();
        foreach (var line in journalLines)
        {
            if (mappedAccountIds.Contains(line.AccountId) ||
                ignoredAccountIds?.Contains(line.AccountId) == true ||
                !IsAccountInStatement(line.AccountType, statementType) ||
                (line.EntryDate < request.FromDate &&
                 statementType != FinancialStatementType.FinancialPosition))
            {
                continue;
            }

            var amount = amounts.GetValueOrDefault(line.AccountId) ??
                new ReportAmount();
            if (statementType == FinancialStatementType.FinancialPosition &&
                line.EntryDate < request.FromDate)
            {
                amount.OpeningDebit += line.Debit;
                amount.OpeningCredit += line.Credit;
            }
            else
            {
                amount.PeriodDebit += line.Debit;
                amount.PeriodCredit += line.Credit;
            }

            amounts[line.AccountId] = amount;
        }

        return journalLines
            .Where(line => amounts.ContainsKey(line.AccountId))
            .GroupBy(line => line.AccountId)
            .Select(group =>
            {
                var first = group.First();
                var amount = amounts[first.AccountId];
                return new FinancialStatementUnmappedAccountResponse(
                    AccountId: first.AccountId,
                    AccountCode: first.AccountCode,
                    AccountName: first.AccountName,
                    AccountType: first.AccountType,
                    OpeningDebit: amount.OpeningDebit,
                    OpeningCredit: amount.OpeningCredit,
                    PeriodDebit: amount.PeriodDebit,
                    PeriodCredit: amount.PeriodCredit,
                    ClosingDebit: amount.ClosingDebit,
                    ClosingCredit: amount.ClosingCredit);
            })
            .Where(item =>
                item.OpeningDebit != 0m ||
                item.OpeningCredit != 0m ||
                item.PeriodDebit != 0m ||
                item.PeriodCredit != 0m)
            .OrderBy(item => item.AccountCode)
            .ToArray();
    }

    private static IReadOnlyList<FinancialStatementReportItemResponse>
        SummarizeItems(
            IReadOnlyList<FinancialStatementReportItemResponse> items) =>
        items
            .GroupBy(item => new
            {
                item.FinancialStatementLineId,
                item.FinancialStatementLineCode,
                item.FinancialStatementLineName
            })
            .Select(group => new FinancialStatementReportItemResponse(
                FinancialStatementLineId: group.Key.FinancialStatementLineId,
                FinancialStatementLineCode: group.Key.FinancialStatementLineCode,
                FinancialStatementLineName: group.Key.FinancialStatementLineName,
                AccountId: null,
                AccountCode: null,
                AccountName: null,
                AccountType: null,
                OpeningDebit: group.Sum(item => item.OpeningDebit),
                OpeningCredit: group.Sum(item => item.OpeningCredit),
                PeriodDebit: group.Sum(item => item.PeriodDebit),
                PeriodCredit: group.Sum(item => item.PeriodCredit),
                ClosingDebit: group.Sum(item => item.ClosingDebit),
                ClosingCredit: group.Sum(item => item.ClosingCredit)))
            .OrderBy(item => item.FinancialStatementLineCode)
            .ToArray();

    private static FinancialStatementReportTotalsResponse BuildReportTotals(
        FinancialStatementType statementType,
        IReadOnlyList<FinancialStatementReportItemResponse> items,
        IReadOnlyList<ReportJournalLine> journalLines,
        FinancialStatementReportRequest request,
        IReadOnlyList<FinancialStatementUnmappedAccountResponse> unmapped)
    {
        var openingDebit = items.Sum(item => item.OpeningDebit);
        var openingCredit = items.Sum(item => item.OpeningCredit);
        var periodDebit = items.Sum(item => item.PeriodDebit);
        var periodCredit = items.Sum(item => item.PeriodCredit);
        var closingDebit = items.Sum(item => item.ClosingDebit);
        var closingCredit = items.Sum(item => item.ClosingCredit);
        var periodNetResult = journalLines
            .Where(line => line.EntryDate >= request.FromDate)
            .Sum(line => line.AccountType == AccountType.Revenue
                ? line.Credit - line.Debit
                : line.AccountType == AccountType.Expense
                    ? line.Debit - line.Credit
                    : 0m);
        var netResult = statementType is
            FinancialStatementType.IncomeStatement or
            FinancialStatementType.FinancialPosition
            ? periodNetResult
            : 0m;
        var totalAssets = statementType == FinancialStatementType.FinancialPosition
            ? items
                .Where(item => item.AccountType == AccountType.Asset)
                .Sum(item => item.ClosingDebit - item.ClosingCredit)
            : 0m;
        var totalLiabilitiesAndEquity =
            statementType == FinancialStatementType.FinancialPosition
                ? items
                    .Where(item => item.AccountType is
                        AccountType.Liability or AccountType.Equity)
                    .Sum(item => item.ClosingCredit - item.ClosingDebit)
                    + periodNetResult
                : 0m;
        var netCashFlow = statementType == FinancialStatementType.CashFlow
            ? items.Sum(item => item.PeriodDebit - item.PeriodCredit)
            : 0m;
        var reportLines = journalLines
            .Where(line => line.EntryDate >= request.FromDate)
            .ToArray();
        var journalIsBalanced = reportLines.Sum(line => line.Debit) ==
            reportLines.Sum(line => line.Credit);

        return new FinancialStatementReportTotalsResponse(
            OpeningDebit: openingDebit,
            OpeningCredit: openingCredit,
            PeriodDebit: periodDebit,
            PeriodCredit: periodCredit,
            ClosingDebit: closingDebit,
            ClosingCredit: closingCredit,
            NetResult: netResult,
            TotalAssets: totalAssets,
            TotalLiabilitiesAndEquity: totalLiabilitiesAndEquity,
            NetCashFlow: netCashFlow,
            IsBalanced: journalIsBalanced && unmapped.Count == 0);
    }

    private static FinancialStatementReportItemResponse ToReportItem(
        ReportMappingRow mapping,
        int accountId,
        ReportJournalLine account,
        ReportAmount amount) =>
        new(
            FinancialStatementLineId: mapping.FinancialStatementLineId,
            FinancialStatementLineCode: mapping.FinancialStatementLineCode,
            FinancialStatementLineName: mapping.FinancialStatementLineName,
            AccountId: accountId,
            AccountCode: account.AccountCode,
            AccountName: account.AccountName,
            AccountType: account.AccountType,
            OpeningDebit: amount.OpeningDebit,
            OpeningCredit: amount.OpeningCredit,
            PeriodDebit: amount.PeriodDebit,
            PeriodCredit: amount.PeriodCredit,
            ClosingDebit: amount.ClosingDebit,
            ClosingCredit: amount.ClosingCredit);

    private static bool IsAccountInStatement(
        AccountType accountType,
        FinancialStatementType statementType) =>
        statementType switch
        {
            FinancialStatementType.FinancialPosition => accountType is
                AccountType.Asset or AccountType.Liability or AccountType.Equity,
            FinancialStatementType.IncomeStatement => accountType is
                AccountType.Revenue or AccountType.Expense,
            FinancialStatementType.CashFlow => true,
            _ => false
        };

    private sealed class ReportJournalLine
    {
        public int JournalEntryId { get; init; }
        public DateOnly EntryDate { get; init; }
        public int AccountId { get; init; }
        public string AccountCode { get; init; } = string.Empty;
        public string AccountName { get; init; } = string.Empty;
        public AccountType AccountType { get; init; }
        public decimal Debit { get; init; }
        public decimal Credit { get; init; }
    }

    private sealed class ReportMappingRow
    {
        public int AccountId { get; init; }
        public int FinancialStatementLineId { get; init; }
        public string FinancialStatementLineCode { get; init; } = string.Empty;
        public string FinancialStatementLineName { get; init; } = string.Empty;
    }

    private sealed class ReportAmount
    {
        public decimal OpeningDebit { get; set; }
        public decimal OpeningCredit { get; set; }
        public decimal PeriodDebit { get; set; }
        public decimal PeriodCredit { get; set; }
        public decimal ClosingDebit => Math.Max(
            OpeningDebit - OpeningCredit + PeriodDebit - PeriodCredit,
            0m);
        public decimal ClosingCredit => Math.Max(
            OpeningCredit - OpeningDebit + PeriodCredit - PeriodDebit,
            0m);
    }
}
