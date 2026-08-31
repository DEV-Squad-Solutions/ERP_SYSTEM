using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.AccountStatementMappings.Jobs;
using MiniErp.Application.Features.AccountStatementMappings;
using MiniErp.Domain.Enums;

namespace MiniErp.Api.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class AccountStatementMappingsController(
    IAccountStatementMappingService accountStatementMappingService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AccountStatementMappingResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] int fiscalYearId,
        [FromQuery] FinancialStatementType statementType,
        CancellationToken cancellationToken)
    {
        var result = await accountStatementMappingService.GetAsync(
            fiscalYearId,
            statementType,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut]
    [ProducesResponseType<IReadOnlyList<AccountStatementMappingResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Replace(
        [FromQuery] int fiscalYearId,
        [FromQuery] FinancialStatementType statementType,
        ReplaceAccountStatementMappingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountStatementMappingService.ReplaceAsync(
            fiscalYearId,
            statementType,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<AccountStatementMappingsRealtimeJob>(
                "Replaced",
                $"{fiscalYearId}:{statementType}",
                realtime => job => job.ExecuteAsync(realtime));
        }

        return this.ToActionResult(result);
    }
}
