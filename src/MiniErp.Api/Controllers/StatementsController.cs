using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.Statements;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class StatementsController(
    IFinancialStatementService statementService)
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
}
