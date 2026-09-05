using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Features.Dashboard;
using MiniErp.Application.Features.ProfitabilityReports;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.Dashboard.DashboardErrors;

namespace MiniErp.Infrastructure.Services.Dashboard;

public sealed class DashboardService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IProfitabilityReportService profitabilityReportService,
    IAccountingReadinessService accountingReadinessService,
    TimeProvider timeProvider)
    : IDashboardService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<DashboardResponse>> GetAsync(
        DashboardFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(
            timeProvider.GetUtcNow().UtcDateTime);
        var fiscalYear = await ResolveFiscalYearAsync(
            filters,
            today,
            cancellationToken);
        if (fiscalYear is null)
        {
            return Result<DashboardResponse>.Failure(FiscalYearNotFound());
        }

        var fromDate = filters.FromDate ?? fiscalYear.StartDate;
        var toDate = filters.ToDate ?? fiscalYear.EndDate;
        if (toDate < fromDate ||
            toDate.DayNumber - fromDate.DayNumber > 365)
        {
            return Result<DashboardResponse>.Failure(InvalidDateRange());
        }

        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;

        var invoices = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.InvoiceDate >= fromDate &&
                invoice.InvoiceDate <= toDate)
            .Select(invoice => new InvoiceDashboardRow
            {
                InvoiceType = invoice.InvoiceType,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                Total = invoice.Total,
                PaidAmount = invoice.PaidAmount,
                BaseTotal = invoice.BaseTotal,
                BasePaidAmountAtInvoiceRate =
                    invoice.BasePaidAmountAtInvoiceRate
            })
            .ToListAsync(cancellationToken);

        var sales = BuildMoneySummary(
            invoices.Where(invoice => invoice.InvoiceType == InvoiceType.Sales),
            invoices.Where(invoice =>
                invoice.InvoiceType == InvoiceType.SalesReturn));
        var purchases = BuildMoneySummary(
            invoices.Where(invoice =>
                invoice.InvoiceType == InvoiceType.Purchase),
            invoices.Where(invoice =>
                invoice.InvoiceType == InvoiceType.PurchaseReturn));

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
                    ToDate: toDate),
                cancellationToken);
        if (profitabilityResult.IsFailure)
        {
            return Result<DashboardResponse>.Failure(
                profitabilityResult.Errors);
        }

        var inventory = await BuildInventorySummaryAsync(cancellationToken);
        var counts = await BuildEntityCountsAsync(
            invoices.Count,
            cancellationToken);
        var invoiceStatus = BuildInvoiceStatus(invoices, today);
        var cashBalances = await BuildCashBalancesAsync(cancellationToken);

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
            invoices,
            fromDate,
            toDate);
        var alerts = await BuildAlertsAsync(
            invoiceStatus,
            inventory,
            accounting,
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
        DashboardFilterRequest filters,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var query = dbContext.FiscalYears
            .AsNoTracking()
            .Where(fiscalYear => fiscalYear.CompanyId == companyId);

        if (!filters.FromDate.HasValue && !filters.ToDate.HasValue)
        {
            var preferred = await query
                .Where(fiscalYear =>
                    fiscalYear.IsCurrent ||
                    (fiscalYear.StartDate <= today &&
                     fiscalYear.EndDate >= today))
                .OrderByDescending(fiscalYear => fiscalYear.IsCurrent)
                .ThenByDescending(fiscalYear => fiscalYear.StartDate)
                .Select(fiscalYear => new FiscalYearProjection(
                    fiscalYear.Id,
                    fiscalYear.Name,
                    fiscalYear.StartDate,
                    fiscalYear.EndDate))
                .FirstOrDefaultAsync(cancellationToken);
            if (preferred is not null)
            {
                return preferred;
            }

            return await query
                .OrderByDescending(fiscalYear => fiscalYear.StartDate)
                .Select(fiscalYear => new FiscalYearProjection(
                    fiscalYear.Id,
                    fiscalYear.Name,
                    fiscalYear.StartDate,
                    fiscalYear.EndDate))
                .FirstOrDefaultAsync(cancellationToken);
        }

        var firstDate = filters.FromDate ?? filters.ToDate!.Value;
        var lastDate = filters.ToDate ?? filters.FromDate!.Value;
        return await query
            .Where(fiscalYear =>
                fiscalYear.StartDate <= firstDate &&
                fiscalYear.EndDate >= lastDate)
            .OrderByDescending(fiscalYear => fiscalYear.IsCurrent)
            .ThenByDescending(fiscalYear => fiscalYear.StartDate)
            .Select(fiscalYear => new FiscalYearProjection(
                fiscalYear.Id,
                fiscalYear.Name,
                fiscalYear.StartDate,
                fiscalYear.EndDate))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static DashboardMoneySummary BuildMoneySummary(
        IEnumerable<InvoiceDashboardRow> source,
        IEnumerable<InvoiceDashboardRow> returns)
    {
        var sourceRows = source.ToArray();
        var returnRows = returns.ToArray();
        var total = sourceRows.Sum(invoice => invoice.BaseTotal);
        var returnTotal = returnRows.Sum(invoice => invoice.BaseTotal);
        var outstanding = sourceRows.Sum(invoice => Math.Max(
            invoice.BaseTotal - invoice.BasePaidAmountAtInvoiceRate,
            0m));

        return new DashboardMoneySummary(
            Total: total,
            Returns: returnTotal,
            Net: total - returnTotal,
            Outstanding: outstanding);
    }

    private async Task<DashboardInventorySummary> BuildInventorySummaryAsync(
        CancellationToken cancellationToken)
    {
        var activeItemCount = await dbContext.Items
            .AsNoTracking()
            .CountAsync(item =>
                item.CompanyId == companyId &&
                item.IsActive,
                cancellationToken);
        var balances = await dbContext.ItemStoreBalances
            .AsNoTracking()
            .Where(balance => balance.CompanyId == companyId)
            .Select(balance => new
            {
                balance.ItemId,
                balance.Quantity,
                balance.InventoryValue,
                balance.Item.IsActive
            })
            .ToListAsync(cancellationToken);
        var itemsWithStockCount = balances
            .Where(balance => balance.IsActive && balance.Quantity > 0m)
            .Select(balance => balance.ItemId)
            .Distinct()
            .Count();
        var pendingCostMovementCount = await dbContext.ItemMovements
            .AsNoTracking()
            .CountAsync(movement =>
                movement.CompanyId == companyId &&
                movement.PendingCostQuantity > 0m,
                cancellationToken);

        return new DashboardInventorySummary(
            CurrentInventoryValue: balances.Sum(balance =>
                balance.InventoryValue),
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
        IEnumerable<InvoiceDashboardRow> invoices,
        DateOnly today)
    {
        var rows = invoices
            .Where(invoice =>
                invoice.InvoiceType == InvoiceType.Sales ||
                invoice.InvoiceType == InvoiceType.Purchase)
            .ToArray();
        var paidCount = rows.Count(invoice =>
            invoice.Total - invoice.PaidAmount <= 0m);
        var unpaidCount = rows.Count(invoice =>
            invoice.PaidAmount <= 0m &&
            invoice.Total > 0m);
        var partiallyPaidCount = rows.Length - paidCount - unpaidCount;
        var overdue = rows.Where(invoice =>
            invoice.DueDate.HasValue &&
            invoice.DueDate.Value < today &&
            invoice.Total - invoice.PaidAmount > 0m)
            .ToArray();

        return new DashboardInvoiceStatusSummary(
            PaidCount: paidCount,
            PartiallyPaidCount: partiallyPaidCount,
            UnpaidCount: unpaidCount,
            OverdueCount: overdue.Length,
            OverdueAmount: overdue.Sum(invoice => Math.Max(
                invoice.BaseTotal - invoice.BasePaidAmountAtInvoiceRate,
                0m)));
    }

    private async Task<IReadOnlyList<DashboardCashBalance>>
        BuildCashBalancesAsync(CancellationToken cancellationToken)
    {
        var cashboxes = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox => cashbox.CompanyId == companyId)
            .Select(cashbox => new
            {
                cashbox.Id,
                cashbox.Currency,
                cashbox.OpeningBalance
            })
            .ToListAsync(cancellationToken);
        var vouchers = await dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.CashboxId.HasValue &&
                voucher.IsPosted)
            .Select(voucher => new
            {
                CashboxId = voucher.CashboxId!.Value,
                voucher.Direction,
                voucher.Amount
            })
            .ToListAsync(cancellationToken);
        var voucherTotals = vouchers
            .GroupBy(voucher => voucher.CashboxId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(voucher =>
                    voucher.Direction == CashDirection.Receipt
                        ? voucher.Amount
                        : -voucher.Amount));

        return cashboxes
            .Select(cashbox => new
            {
                cashbox.Currency,
                Balance = cashbox.OpeningBalance +
                    voucherTotals.GetValueOrDefault(cashbox.Id)
            })
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
            IEnumerable<InvoiceDashboardRow> invoices,
            DateOnly fromDate,
            DateOnly toDate)
    {
        var rows = invoices.ToArray();
        var result = new List<DashboardMonthlyActivity>();
        var month = new DateOnly(fromDate.Year, fromDate.Month, 1);
        var lastMonth = new DateOnly(toDate.Year, toDate.Month, 1);
        while (month <= lastMonth)
        {
            var monthlyRows = rows.Where(invoice =>
                invoice.InvoiceDate.Year == month.Year &&
                invoice.InvoiceDate.Month == month.Month);
            var sales = monthlyRows.Sum(invoice =>
                invoice.InvoiceType == InvoiceType.Sales
                    ? invoice.BaseTotal
                    : invoice.InvoiceType == InvoiceType.SalesReturn
                        ? -invoice.BaseTotal
                        : 0m);
            var purchases = monthlyRows.Sum(invoice =>
                invoice.InvoiceType == InvoiceType.Purchase
                    ? invoice.BaseTotal
                    : invoice.InvoiceType == InvoiceType.PurchaseReturn
                        ? -invoice.BaseTotal
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

    private sealed class InvoiceDashboardRow
    {
        public InvoiceType InvoiceType { get; init; }

        public DateOnly InvoiceDate { get; init; }

        public DateOnly? DueDate { get; init; }

        public decimal Total { get; init; }

        public decimal PaidAmount { get; init; }

        public decimal BaseTotal { get; init; }

        public decimal BasePaidAmountAtInvoiceRate { get; init; }
    }
}
