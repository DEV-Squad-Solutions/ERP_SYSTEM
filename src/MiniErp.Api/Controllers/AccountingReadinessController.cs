using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Features.AccountingReadiness;

namespace MiniErp.Api.Controllers;

[Authorize(Roles = "Admin")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class AccountingReadinessController(
    IAccountingReadinessService accountingReadinessService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<AccountingReadinessResponse>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] int fiscalYearId,
        CancellationToken cancellationToken)
    {
        var result = await accountingReadinessService.GetAsync(
            fiscalYearId,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("backfill")]
    [ProducesResponseType<AccountingBackfillResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Backfill(
        [FromQuery] int fiscalYearId,
        CancellationToken cancellationToken)
    {
        var result = await accountingReadinessService.BackfillAsync(
            fiscalYearId,
            cancellationToken);
        return this.ToActionResult(result);
    }
}
