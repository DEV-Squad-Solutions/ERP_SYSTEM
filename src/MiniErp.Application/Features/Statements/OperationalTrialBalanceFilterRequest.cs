namespace MiniErp.Application.Features.Statements;

public enum OperationalTrialBalanceViewMode
{
    Summary = 1,
    Detailed = 2
}

public enum OperationalTrialBalanceCategory
{
    Cashbox = 1,
    Partner = 2,
    Driver = 3,
    Employee = 4,
    Revenue = 5,
    Expense = 6
}

public sealed record OperationalTrialBalanceFilterRequest(
    DateOnly FromDate,
    DateOnly ToDate,
    OperationalTrialBalanceViewMode ViewMode =
        OperationalTrialBalanceViewMode.Detailed,
    OperationalTrialBalanceCategory? Category = null,
    bool IncludeZeroBalances = false);
