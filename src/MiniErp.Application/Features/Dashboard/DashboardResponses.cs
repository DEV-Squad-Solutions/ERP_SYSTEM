using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Dashboard;

public sealed record DashboardMoneySummary(
    decimal Total,
    decimal Returns,
    decimal Net,
    decimal Outstanding);

public sealed record DashboardProfitabilitySummary(
    decimal NetRevenue,
    decimal RecognizedCost,
    decimal? GrossProfit,
    decimal? GrossMarginPercentage,
    int PendingInvoiceCount,
    decimal PendingCostQuantity);

public sealed record DashboardInventorySummary(
    decimal CurrentInventoryValue,
    int ActiveItemCount,
    int ItemsWithStockCount,
    int ZeroStockItemCount,
    int PendingCostMovementCount);

public sealed record DashboardEntityCounts(
    int CustomerCount,
    int SupplierCount,
    int EmployeeCount,
    int DriverCount,
    int InvoiceCount);

public sealed record DashboardInvoiceStatusSummary(
    int PaidCount,
    int PartiallyPaidCount,
    int UnpaidCount,
    int OverdueCount,
    decimal OverdueAmount);

public sealed record DashboardCashBalance(
    CurrencyCode Currency,
    int CashboxCount,
    decimal CurrentBalance);

public sealed record DashboardAccountingSummary(
    bool IsReady,
    int IssueCount,
    int MissingJournalSources,
    int UnbalancedJournals,
    int PendingInventoryCosts,
    int MissingOrInvalidMappings);

public sealed record DashboardMonthlyActivity(
    int Year,
    int Month,
    decimal Sales,
    decimal Purchases);

public sealed record DashboardAlert(
    string Code,
    string Severity,
    int Count,
    string Message);

public sealed record DashboardResponse(
    int FiscalYearId,
    string FiscalYearName,
    DateOnly FromDate,
    DateOnly ToDate,
    CurrencyCode BaseCurrency,
    DashboardMoneySummary Sales,
    DashboardMoneySummary Purchases,
    DashboardProfitabilitySummary Profitability,
    DashboardInventorySummary Inventory,
    DashboardEntityCounts Counts,
    DashboardInvoiceStatusSummary InvoiceStatus,
    IReadOnlyList<DashboardCashBalance> CashBalances,
    DashboardAccountingSummary Accounting,
    IReadOnlyList<DashboardMonthlyActivity> MonthlyActivity,
    IReadOnlyList<DashboardAlert> Alerts);
