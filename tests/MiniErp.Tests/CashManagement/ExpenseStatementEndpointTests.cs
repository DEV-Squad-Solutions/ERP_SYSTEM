using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Controllers;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ProfitabilityReports;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.CashManagement;

public sealed class ExpenseStatementEndpointTests
{
    [Fact]
    public void ExpensesRouteIsRegistered()
    {
        var method = typeof(StatementsController).GetMethod(
            nameof(StatementsController.GetExpenseStatement),
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
        var route = Assert.Single(
            method!.GetCustomAttributes<HttpGetAttribute>());
        Assert.Equal("expenses", route.Template);
    }

    [Fact]
    public async Task ExpenseEndpointForcesExpenseCategory()
    {
        var fromDate = new DateOnly(2026, 6, 1);
        var toDate = new DateOnly(2026, 6, 30);
        var statementService = new CapturingStatementService();
        var controller = new StatementsController(
            statementService,
            new StubProfitabilityReportService());

        var result = await controller.GetExpenseStatement(
            new OperationalTrialBalanceFilterRequest(
                FromDate: fromDate,
                ToDate: toDate,
                ViewMode: OperationalTrialBalanceViewMode.Detailed,
                Category: OperationalTrialBalanceCategory.Revenue,
                IncludeZeroBalances: true),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(statementService.CapturedFilters);
        Assert.Equal(
            OperationalTrialBalanceCategory.Expense,
            statementService.CapturedFilters!.Category);
        Assert.Equal(fromDate, statementService.CapturedFilters.FromDate);
        Assert.Equal(toDate, statementService.CapturedFilters.ToDate);
        Assert.True(statementService.CapturedFilters.IncludeZeroBalances);
        var response = Assert.IsType<OperationalTrialBalanceResponse>(
            ok.Value);
        Assert.All(
            response.Items,
            item => Assert.Equal(
                OperationalTrialBalanceCategory.Expense,
                item.Category));
    }

    private sealed class CapturingStatementService : IFinancialStatementService
    {
        public OperationalTrialBalanceFilterRequest? CapturedFilters { get; private set; }

        public Task<Result<OperationalTrialBalanceResponse>>
            GetOperationalTrialBalanceAsync(
                OperationalTrialBalanceFilterRequest filters,
                CancellationToken cancellationToken = default)
        {
            CapturedFilters = filters;
            var item = new OperationalTrialBalanceItemResponse(
                Category: OperationalTrialBalanceCategory.Expense,
                CategoryName: "Expense",
                AccountId: 2,
                AccountCode: "EXP-1",
                AccountName: "Expense account",
                OpeningDebit: 0m,
                OpeningCredit: 0m,
                PeriodDebit: 77m,
                PeriodCredit: 0m,
                ClosingDebit: 77m,
                ClosingCredit: 0m);
            var response = new OperationalTrialBalanceResponse(
                FromDate: filters.FromDate,
                ToDate: filters.ToDate,
                BaseCurrency: CurrencyCode.USD,
                ViewMode: filters.ViewMode,
                Items: [item],
                Totals: new OperationalTrialBalanceTotalsResponse(
                    OpeningDebit: 0m,
                    OpeningCredit: 0m,
                    PeriodDebit: 77m,
                    PeriodCredit: 0m,
                    ClosingDebit: 77m,
                    ClosingCredit: 0m));
            return Task.FromResult(Result<OperationalTrialBalanceResponse>.Success(response));
        }

        public Task<Result<CashboxStatementResponse>> GetCashboxStatementAsync(
            PaginationRequest pagination,
            CashboxStatementFilterRequest filters,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<PartnerStatementResponse>> GetPartnerStatementAsync(
            PaginationRequest pagination,
            PartnerStatementFilterRequest filters,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<DriverStatementResponse>> GetDriverStatementAsync(
            PaginationRequest pagination,
            DriverStatementFilterRequest filters,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<ContainerStoreStatementResponse>>
            GetContainerStoreStatementAsync(
                PaginationRequest pagination,
                ContainerStoreStatementFilterRequest filters,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<TrialBalanceResponse>> GetTrialBalanceAsync(
            TrialBalanceFilterRequest filters,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<FinancialStatementReportResponse>>
            GetFinancialStatementReportAsync(
                FinancialStatementType statementType,
                FinancialStatementReportRequest request,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<EmployeeStatementResponse>> GetEmployeeStatementAsync(
            PaginationRequest pagination,
            EmployeeStatementFilterRequest filters,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<EmployeeAccountBalanceResponse>> GetEmployeeBalanceAsync(
            int employeeId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<EmployeeAccountSummaryResponse>> GetEmployeeAccountSummaryAsync(
            int employeeId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubProfitabilityReportService : IProfitabilityReportService
    {
        public Task<Result<InvoiceProfitabilityListResponse>> GetInvoicesAsync(
            PaginationRequest pagination,
            ProfitabilityReportFilterRequest filters,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<InvoiceProfitabilityResponse>> GetInvoiceDetailsAsync(
            int invoiceId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<ItemProfitabilityListResponse>> GetItemsAsync(
            PaginationRequest pagination,
            ProfitabilityReportFilterRequest filters,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
