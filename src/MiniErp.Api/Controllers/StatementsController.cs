using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.ProfitabilityReports;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class StatementsController(
    IFinancialStatementService statementService,
    IProfitabilityReportService profitabilityReportService)
    : ApiControllerBase
{
    [HttpGet("cashbox")]
    [ProducesResponseType<CashboxStatementResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCashboxStatement(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] CashboxStatementFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await statementService.GetCashboxStatementAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("partner")]
    [ProducesResponseType<PartnerStatementResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPartnerStatement(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] PartnerStatementFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await statementService.GetPartnerStatementAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("driver")]
    [ProducesResponseType<DriverStatementResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDriverStatement(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] DriverStatementFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await statementService.GetDriverStatementAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("container-store")]
    [ProducesResponseType<ContainerStoreStatementResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContainerStoreStatement(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] ContainerStoreStatementFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await statementService.GetContainerStoreStatementAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("operational-trial-balance")]
    [ProducesResponseType<OperationalTrialBalanceResponse>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOperationalTrialBalance(
        [FromQuery] OperationalTrialBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await statementService
            .GetOperationalTrialBalanceAsync(
                filters,
                cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("trial-balance")]
    [ProducesResponseType<TrialBalanceResponse>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrialBalance(
        [FromQuery] TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await statementService.GetTrialBalanceAsync(
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("income-statement")]
    [ProducesResponseType<FinancialStatementReportResponse>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncomeStatement(
        [FromQuery] FinancialStatementReportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await statementService.GetFinancialStatementReportAsync(
            FinancialStatementType.IncomeStatement,
            request,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("financial-position")]
    [ProducesResponseType<FinancialStatementReportResponse>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFinancialPosition(
        [FromQuery] FinancialStatementReportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await statementService.GetFinancialStatementReportAsync(
            FinancialStatementType.FinancialPosition,
            request,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("cash-flow")]
    [ProducesResponseType<FinancialStatementReportResponse>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCashFlow(
        [FromQuery] FinancialStatementReportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await statementService.GetFinancialStatementReportAsync(
            FinancialStatementType.CashFlow,
            request,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("employee")]
    [ProducesResponseType<EmployeeStatementResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmployeeStatement(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] EmployeeStatementFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await statementService.GetEmployeeStatementAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("employee/{employeeId:int}/balance")]
    [ProducesResponseType<EmployeeAccountBalanceResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmployeeBalance(
        int employeeId,
        CancellationToken cancellationToken)
    {
        var result = await statementService.GetEmployeeBalanceAsync(
            employeeId,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("employee/{employeeId:int}/account")]
    [ProducesResponseType<EmployeeAccountSummaryResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmployeeAccountSummary(
        int employeeId,
        CancellationToken cancellationToken)
    {
        var result = await statementService.GetEmployeeAccountSummaryAsync(
            employeeId,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("profitability/invoices")]
    [ProducesResponseType<InvoiceProfitabilityListResponse>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoiceProfitability(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] ProfitabilityReportFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await profitabilityReportService.GetInvoicesAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("profitability/invoices/{invoiceId:int}")]
    [ProducesResponseType<InvoiceProfitabilityResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoiceProfitabilityDetails(
        int invoiceId,
        CancellationToken cancellationToken)
    {
        var result = await profitabilityReportService
            .GetInvoiceDetailsAsync(
                invoiceId,
                cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("profitability/items")]
    [ProducesResponseType<ItemProfitabilityListResponse>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItemProfitability(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] ProfitabilityReportFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await profitabilityReportService.GetItemsAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }
}
