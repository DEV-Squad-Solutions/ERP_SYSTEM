using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Statements;

public sealed record TrialBalanceItemResponse(
    int? AccountId,
    string? AccountCode,
    string AccountName,
    AccountType? AccountType,
    bool IsUnclassified,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit);

public sealed record TrialBalanceTotalsResponse(
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit);

public sealed record TrialBalanceResponse(
    int FiscalYearId,
    string FiscalYearName,
    DateOnly FromDate,
    DateOnly ToDate,
    CurrencyCode BaseCurrency,
    TrialBalanceViewMode ViewMode,
    TrialBalanceAdjustmentView AdjustmentView,
    bool IsOperationalOnly,
    IReadOnlyList<TrialBalanceItemResponse> Items,
    TrialBalanceTotalsResponse Totals);
