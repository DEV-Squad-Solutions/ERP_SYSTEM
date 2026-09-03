using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Statements;

public sealed record FinancialStatementReportItemResponse(
    int? FinancialStatementLineId,
    string? FinancialStatementLineCode,
    string FinancialStatementLineName,
    int? AccountId,
    string? AccountCode,
    string? AccountName,
    AccountType? AccountType,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit);

public sealed record FinancialStatementUnmappedAccountResponse(
    int AccountId,
    string AccountCode,
    string AccountName,
    AccountType AccountType,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit);

public sealed record FinancialStatementReportTotalsResponse(
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit,
    decimal NetResult,
    decimal TotalAssets,
    decimal TotalLiabilitiesAndEquity,
    decimal NetCashFlow,
    bool IsBalanced);

public sealed record FinancialStatementReportResponse(
    FinancialStatementType StatementType,
    int FiscalYearId,
    string FiscalYearName,
    DateOnly FromDate,
    DateOnly ToDate,
    CurrencyCode BaseCurrency,
    TrialBalanceViewMode ViewMode,
    TrialBalanceAdjustmentView AdjustmentView,
    bool IsReadyForReporting,
    IReadOnlyList<FinancialStatementReportItemResponse> Items,
    FinancialStatementReportTotalsResponse Totals,
    IReadOnlyList<FinancialStatementUnmappedAccountResponse> UnmappedAccounts);
