using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Statements;

public sealed record OperationalTrialBalanceItemResponse(
    OperationalTrialBalanceCategory Category,
    string CategoryName,
    int? AccountId,
    string? AccountCode,
    string AccountName,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit);

public sealed record OperationalTrialBalanceTotalsResponse(
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit);

public sealed record OperationalTrialBalanceResponse(
    DateOnly FromDate,
    DateOnly ToDate,
    CurrencyCode BaseCurrency,
    OperationalTrialBalanceViewMode ViewMode,
    IReadOnlyList<OperationalTrialBalanceItemResponse> Items,
    OperationalTrialBalanceTotalsResponse Totals);
