using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.DriverOpeningBalances.Jobs;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.DriverOpeningBalances;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class DriverOpeningBalancesController(IDriverOpeningBalanceService driverOpeningBalanceService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<DriverOpeningBalanceResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] int? fiscalYearId,
        [FromQuery] int? driverId,
        CancellationToken cancellationToken)
    {
        var result = await driverOpeningBalanceService.GetAllAsync(
            pagination, fiscalYearId, driverId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<DriverOpeningBalanceResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id, [FromQuery] int? fiscalYearId, CancellationToken cancellationToken)
    {
        var result = await driverOpeningBalanceService.GetByIdAsync(id, fiscalYearId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Roles = ApplicationRoles.Admin)]
    [ProducesResponseType<DriverOpeningBalanceResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(DriverOpeningBalanceRequest request, CancellationToken cancellationToken)
    {
        var result = await driverOpeningBalanceService.AddAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToProblem(result.Errors);
        }

        TryEnqueueRealtime<DriverOpeningBalancesRealtimeJob>(
            "Added", result.Value.Id, realtime => job => job.ExecuteAsync(realtime));
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value.Id, fiscalYearId = result.Value.FiscalYearId },
            result.Value);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = ApplicationRoles.Admin)]
    [ProducesResponseType<DriverOpeningBalanceResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, DriverOpeningBalanceUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await driverOpeningBalanceService.UpdateAsync(id, request, cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<DriverOpeningBalancesRealtimeJob>(
                "Updated", id, realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = ApplicationRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, [FromQuery] string? rowVersion, CancellationToken cancellationToken)
    {
        var bytes = new byte[8];
        var decoded = rowVersion is not null &&
            Convert.TryFromBase64String(rowVersion, bytes, out var length) &&
            length == bytes.Length;
        var result = await driverOpeningBalanceService.DeleteAsync(id, decoded ? bytes : null, cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<DriverOpeningBalancesRealtimeJob>(
                "Deleted", id, realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }
}
