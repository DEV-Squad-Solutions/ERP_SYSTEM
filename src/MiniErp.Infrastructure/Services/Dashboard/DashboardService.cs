using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Features.Dashboard;
using MiniErp.Application.Features.ProfitabilityReports;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Monitoring;
using MiniErp.Infrastructure.Services.Statements;
using static MiniErp.Application.Features.Dashboard.DashboardErrors;

namespace MiniErp.Infrastructure.Services.Dashboard;

public sealed class DashboardService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IProfitabilityReportService profitabilityReportService,
    IAccountingReadinessService accountingReadinessService,
    TimeProvider timeProvider,
    ILogger<DashboardService>? logger = null)
    : IDashboardService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<DashboardResponse>> GetAsync(
        DashboardFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var result = await BuildDashboardAsync(filters, cancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        ReportingMetrics.DashboardDuration.Record(
            elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>(
                "outcome",
                result.IsSuccess ? "success" : "failure"));
        logger?.LogInformation(
            "Dashboard query completed for company {CompanyId} in {ElapsedMilliseconds} ms with outcome {Outcome}.",
            companyId,
            elapsed.TotalMilliseconds,
            result.IsSuccess ? "Success" : "Failure");
        return result;
    }

    private async Task<Result<DashboardResponse>> BuildDashboardAsync(
        DashboardFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(
            timeProvider.GetUtcNow().UtcDateTime);
        var fiscalYear = await ResolveFiscalYearAsync(
            cancellationToken);
        if (fiscalYear is null)
        {
            return Result<DashboardResponse>.Failure(FiscalYearNotFound());
        }

        var fromDate = filters.FromDate ?? fiscalYear.StartDate;
        var toDate = filters.ToDate ?? fiscalYear.EndDate;
        if (fromDate < fiscalYear.StartDate ||
            toDate > fiscalYear.EndDate ||
            toDate < fromDate ||
            toDate.DayNumber - fromDate.DayNumber > 365)
        {
            return Result<DashboardResponse>.Failure(InvalidDateRange());
        }

        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;

        var invoiceActivity = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.FiscalYearId == fiscalYear.Id &&
                invoice.InvoiceDate >= fromDate &&
                invoice.InvoiceDate <= toDate)
            .GroupBy(invoice => new
            {
                invoice.InvoiceType,
                Year = invoice.InvoiceDate.Year,
                Month = invoice.InvoiceDate.Month
            })
            .Select(group => new InvoiceActivityAggregate
            {
                InvoiceType = group.Key.InvoiceType,
                Year = group.Key.Year,
                Month = group.Key.Month,
                InvoiceCount = group.Count(),
                BaseTotal = group.Sum(invoice => invoice.BaseTotal),
                Outstanding = group.Sum(invoice =>
                    invoice.BaseTotal - invoice.BasePaidAmountAtInvoiceRate > 0m
                        ? invoice.BaseTotal -
                            invoice.BasePaidAmountAtInvoiceRate
                        : 0m),
                PaidCount = group.Count(invoice =>
                    invoice.Total - invoice.PaidAmount <= 0m),
                UnpaidCount = group.Count(invoice =>
                    invoice.PaidAmount <= 0m && invoice.Total > 0m),
                OverdueCount = group.Count(invoice =>
                    invoice.DueDate.HasValue &&
                    invoice.DueDate.Value < today &&
                    invoice.Total - invoice.PaidAmount > 0m),
                OverdueAmount = group.Sum(invoice =>
                    invoice.DueDate.HasValue &&
                    invoice.DueDate.Value < today &&
                    invoice.Total - invoice.PaidAmount > 0m &&
                    invoice.BaseTotal -
                        invoice.BasePaidAmountAtInvoiceRate > 0m
                        ? invoice.BaseTotal -
                            invoice.BasePaidAmountAtInvoiceRate
                        : 0m)
            })
            .ToListAsync(cancellationToken);

        var sales = BuildMoneySummary(
            invoiceActivity,
            InvoiceType.Sales,
            InvoiceType.SalesReturn);
        var purchases = BuildMoneySummary(
            invoiceActivity,
            InvoiceType.Purchase,
            InvoiceType.PurchaseReturn);

        var profitabilityResult = await profitabilityReportService
            .GetInvoicesAsync(
                new PaginationRequest
                {
                    PageNumber = 1,
                    PageSize = 1
                },
                new ProfitabilityReportFilterRequest(
                    IncludeReturns: true,
                    FromDate: fromDate,
                    ToDate: toDate,
                    FiscalYearId: fiscalYear.Id),
                cancellationToken);
        if (profitabilityResult.IsFailure)
        {
            return Result<DashboardResponse>.Failure(
                profitabilityResult.Errors);
        }

        var inventory = await BuildInventorySummaryAsync(
            fiscalYear.Id,
            toDate,
            cancellationToken);
        var counts = await BuildEntityCountsAsync(
            invoiceActivity.Sum(row => row.InvoiceCount),
            cancellationToken);
        var invoiceStatus = BuildInvoiceStatus(invoiceActivity);
        var cashBalances = await BuildCashBalancesAsync(
            fiscalYear.Id,
            toDate,
            cancellationToken);

        var readinessResult = await accountingReadinessService.GetAsync(
            fiscalYear.Id,
            cancellationToken);
        if (readinessResult.IsFailure)
        {
            return Result<DashboardResponse>.Failure(readinessResult.Errors);
        }

        var readiness = readinessResult.Value;
        var accounting = new DashboardAccountingSummary(
            IsReady: readiness.IsReady,
            IssueCount: readiness.Issues.Count,
            MissingJournalSources: readiness.MissingJournalSources,
            UnbalancedJournals: readiness.UnbalancedAutomaticJournals,
            PendingInventoryCosts: readiness.PendingInventoryCosts,
            MissingOrInvalidMappings: readiness.MissingOrInvalidMappings);
        var profitability = profitabilityResult.Value.Summary;
        var profitabilitySummary = new DashboardProfitabilitySummary(
            NetRevenue: profitability.NetRevenue,
            RecognizedCost: profitability.RecognizedCost,
            GrossProfit: profitability.GrossProfit,
            GrossMarginPercentage: profitability.GrossMarginPercentage,
            PendingInvoiceCount: profitability.PendingInvoiceCount,
            PendingCostQuantity: profitability.PendingCostQuantity);
        var monthlyActivity = BuildMonthlyActivity(
            invoiceActivity,
            fromDate,
            toDate);
        var alerts = await BuildAlertsAsync(
            invoiceStatus,
            inventory,
            accounting,
            fiscalYear.Id,
            cancellationToken);

        return Result<DashboardResponse>.Success(
            new DashboardResponse(
                FiscalYearId: fiscalYear.Id,
                FiscalYearName: fiscalYear.Name,
                FromDate: fromDate,
                ToDate: toDate,
                BaseCurrency: baseCurrency,
                Sales: sales,
                Purchases: purchases,
                Profitability: profitabilitySummary,
                Inventory: inventory,
                Counts: counts,
                InvoiceStatus: invoiceStatus,
                CashBalances: cashBalances,
                Accounting: accounting,
                MonthlyActivity: monthlyActivity,
                Alerts: alerts));
    }

    private async Task<FiscalYearProjection?> ResolveFiscalYearAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.FiscalYears
            .AsNoTracking()
            .Where(fiscalYear =>
                fiscalYear.CompanyId == companyId &&
                fiscalYear.IsCurrent)
            .Select(fiscalYear => new FiscalYearProjection(
                Id: fiscalYear.Id,
                Name: fiscalYear.Name,
                StartDate: fiscalYear.StartDate,
                EndDate: fiscalYear.EndDate))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static DashboardMoneySummary BuildMoneySummary(
        IEnumerable<InvoiceActivityAggregate> rows,
        InvoiceType sourceType,
        InvoiceType returnType)
    {
        var materializedRows = rows.ToArray();
        var total = materializedRows
            .Where(row => row.InvoiceType == sourceType)
            .Sum(row => row.BaseTotal);
        var returnTotal = materializedRows
            .Where(row => row.InvoiceType == returnType)
            .Sum(row => row.BaseTotal);
        var outstanding = materializedRows
            .Where(row => row.InvoiceType == sourceType)
            .Sum(row => row.Outstanding);

        return new DashboardMoneySummary(
            Total: total,
            Returns: returnTotal,
            Net: total - returnTotal,
            Outstanding: outstanding);
    }

    private async Task<DashboardInventorySummary> BuildInventorySummaryAsync(
        int fiscalYearId,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var activeItemCount = await dbContext.Items
            .AsNoTracking()
            .CountAsync(item =>
                item.CompanyId == companyId &&
                item.IsActive,
                cancellationToken);
        var latestMovements = dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.MovementDate <= toDate &&
                !dbContext.ItemMovements.Any(later =>
                    later.CompanyId == companyId &&
                    later.StoreId == movement.StoreId &&
                    later.ItemId == movement.ItemId &&
                    later.MovementDate <= toDate &&
                    (later.MovementDate > movement.MovementDate ||
                     later.MovementDate == movement.MovementDate &&
                     later.Id > movement.Id)));
        var currentInventoryValue = await latestMovements
            .SumAsync(movement => (decimal?)movement.InventoryValueAfter,
                cancellationToken) ?? 0m;
        var itemsWithStockCount = await latestMovements
            .Where(movement =>
                movement.Item.IsActive &&
                movement.QuantityAfter > 0m)
            .Select(movement => movement.ItemId)
            .Distinct()
            .CountAsync(cancellationToken);
        var pendingCostMovementCount = await dbContext.ItemMovements
            .AsNoTracking()
            .CountAsync(movement =>
                movement.CompanyId == companyId &&
                movement.FiscalYearId == fiscalYearId &&
                movement.PendingCostQuantity > 0m,
                cancellationToken);

        return new DashboardInventorySummary(
            CurrentInventoryValue: currentInventoryValue,
            ActiveItemCount: activeItemCount,
            ItemsWithStockCount: itemsWithStockCount,
            ZeroStockItemCount: Math.Max(
                activeItemCount - itemsWithStockCount,
                0),
            PendingCostMovementCount: pendingCostMovementCount);
    }

    private async Task<DashboardEntityCounts> BuildEntityCountsAsync(
        int invoiceCount,
        CancellationToken cancellationToken)
    {
        var businessPartnerCount = await dbContext.BusinessPartners
            .AsNoTracking()
            .CountAsync(
                partner => partner.CompanyId == companyId,
                cancellationToken);
        var employeeCount = await dbContext.Employees
            .AsNoTracking()
            .CountAsync(employee =>
                employee.CompanyId == companyId &&
                employee.IsActive,
                cancellationToken);
        var driverCount = await dbContext.Drivers
            .AsNoTracking()
            .CountAsync(driver =>
                driver.CompanyId == companyId &&
                driver.IsActive,
                cancellationToken);

        return new DashboardEntityCounts(
            BusinessPartnerCount: businessPartnerCount,
            EmployeeCount: employeeCount,
            DriverCount: driverCount,
            InvoiceCount: invoiceCount);
    }

    private static DashboardInvoiceStatusSummary BuildInvoiceStatus(
        IEnumerable<InvoiceActivityAggregate> invoiceActivity)
    {
        var rows = invoiceActivity
            .Where(row =>
                row.InvoiceType == InvoiceType.Sales ||
                row.InvoiceType == InvoiceType.Purchase)
            .ToArray();
        var totalCount = rows.Sum(row => row.InvoiceCount);
        var paidCount = rows.Sum(row => row.PaidCount);
        var unpaidCount = rows.Sum(row => row.UnpaidCount);

        return new DashboardInvoiceStatusSummary(
            PaidCount: paidCount,
            PartiallyPaidCount: totalCount - paidCount - unpaidCount,
            UnpaidCount: unpaidCount,
            OverdueCount: rows.Sum(row => row.OverdueCount),
            OverdueAmount: rows.Sum(row => row.OverdueAmount));
    }

    private async Task<IReadOnlyList<DashboardCashBalance>>
        BuildCashBalancesAsync(
            int fiscalYearId,
            DateOnly toDate,
            CancellationToken cancellationToken)
    {
        var cashboxTotals = PostedJournalLedgerLines
            .Create(dbContext, companyId)
            .Where(line => line.PartyType == JournalPartyType.Cashbox &&
                line.PartyId.HasValue &&
                line.JournalEntry.FiscalYearId == fiscalYearId &&
                line.JournalEntry.EntryDate <= toDate)
            .GroupBy(line => line.PartyId!.Value)
            .Select(group => new
            {
                CashboxId = group.Key,
                Total = (decimal?)group.Sum(line =>
                    line.TransactionDebit - line.TransactionCredit)
            });

        var balances = await (
                from cashbox in dbContext.Cashboxes.AsNoTracking()
                where cashbox.CompanyId == companyId
                join cashboxTotal in cashboxTotals
                    on cashbox.Id equals cashboxTotal.CashboxId
                    into matchingTotals
                from cashboxTotal in matchingTotals.DefaultIfEmpty()
                select new
                {
                    cashbox.Currency,
                    Balance = cashboxTotal.Total ?? 0m
                })
            .ToListAsync(cancellationToken);

        return balances
            .GroupBy(cashbox => cashbox.Currency)
            .OrderBy(group => group.Key)
            .Select(group => new DashboardCashBalance(
                Currency: group.Key,
                CashboxCount: group.Count(),
                CurrentBalance: group.Sum(cashbox => cashbox.Balance)))
            .ToArray();
    }

    private static IReadOnlyList<DashboardMonthlyActivity>
        BuildMonthlyActivity(
            IEnumerable<InvoiceActivityAggregate> invoiceActivity,
            DateOnly fromDate,
            DateOnly toDate)
    {
        var rows = invoiceActivity.ToArray();
        var result = new List<DashboardMonthlyActivity>();
        var month = new DateOnly(fromDate.Year, fromDate.Month, 1);
        var lastMonth = new DateOnly(toDate.Year, toDate.Month, 1);
        while (month <= lastMonth)
        {
            var monthlyRows = rows.Where(row =>
                row.Year == month.Year && row.Month == month.Month);
            var sales = monthlyRows.Sum(row =>
                row.InvoiceType == InvoiceType.Sales
                    ? row.BaseTotal
                    : row.InvoiceType == InvoiceType.SalesReturn
                        ? -row.BaseTotal
                        : 0m);
            var purchases = monthlyRows.Sum(row =>
                row.InvoiceType == InvoiceType.Purchase
                    ? row.BaseTotal
                    : row.InvoiceType == InvoiceType.PurchaseReturn
                        ? -row.BaseTotal
                        : 0m);
            result.Add(new DashboardMonthlyActivity(
                Year: month.Year,
                Month: month.Month,
                Sales: sales,
                Purchases: purchases));
            month = month.AddMonths(1);
        }

        return result;
    }

    private async Task<IReadOnlyList<DashboardAlert>> BuildAlertsAsync(
        DashboardInvoiceStatusSummary invoiceStatus,
        DashboardInventorySummary inventory,
        DashboardAccountingSummary accounting,
        int fiscalYearId,
        CancellationToken cancellationToken)
    {
        var alerts = new List<DashboardAlert>();
        if (invoiceStatus.OverdueCount > 0)
        {
            alerts.Add(new DashboardAlert(
                Code: "OverdueInvoices",
                Severity: "Warning",
                Count: invoiceStatus.OverdueCount,
                Message: "توجد فواتير مستحقة لم تُسدد بالكامل."));
        }

        if (inventory.PendingCostMovementCount > 0)
        {
            alerts.Add(new DashboardAlert(
                Code: "PendingInventoryCosts",
                Severity: "Error",
                Count: inventory.PendingCostMovementCount,
                Message: "توجد حركات مخزون بتكلفة معلقة تؤثر على الربحية."));
        }

        var draftVoucherCount = await dbContext.CashVouchers
            .AsNoTracking()
            .CountAsync(voucher =>
                voucher.CompanyId == companyId &&
                voucher.FiscalYearId == fiscalYearId &&
                !voucher.IsPosted,
                cancellationToken);
        if (draftVoucherCount > 0)
        {
            alerts.Add(new DashboardAlert(
                Code: "DraftCashVouchers",
                Severity: "Info",
                Count: draftVoucherCount,
                Message: "توجد سندات قبض أو صرف ما زالت مسودة."));
        }

        if (!accounting.IsReady)
        {
            alerts.Add(new DashboardAlert(
                Code: "AccountingReadiness",
                Severity: "Error",
                Count: accounting.IssueCount,
                Message: "توجد مشكلات تمنع اكتمال الجاهزية المحاسبية."));
        }

        return alerts;
    }

    private sealed record FiscalYearProjection(
        int Id,
        string Name,
        DateOnly StartDate,
        DateOnly EndDate);

    private sealed class InvoiceActivityAggregate
    {
        public InvoiceType InvoiceType { get; init; }

        public int Year { get; init; }

        public int Month { get; init; }

        public int InvoiceCount { get; init; }

        public decimal BaseTotal { get; init; }

        public decimal Outstanding { get; init; }

        public int PaidCount { get; init; }

        public int UnpaidCount { get; init; }

        public int OverdueCount { get; init; }

        public decimal OverdueAmount { get; init; }
    }
}
