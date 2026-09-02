using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.AccountMappings.Jobs;
using MiniErp.Application.Features.AccountMappings;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class AccountMappingsController(
    IAccountMappingService accountMappingService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AccountMappingResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] int fiscalYearId,
        CancellationToken cancellationToken)
    {
        var result = await accountMappingService.GetAsync(
            fiscalYearId,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut]
    [ProducesResponseType<IReadOnlyList<AccountMappingResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Replace(
        [FromQuery] int fiscalYearId,
        ReplaceAccountMappingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountMappingService.ReplaceAsync(
            fiscalYearId,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<AccountMappingsRealtimeJob>(
                "Replaced",
                fiscalYearId,
                realtime => job => job.ExecuteAsync(realtime));
        }

        return this.ToActionResult(result);
    }
}
