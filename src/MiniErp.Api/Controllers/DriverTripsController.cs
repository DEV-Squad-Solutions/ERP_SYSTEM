using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.DriverTrips.Jobs;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.DriverTrips;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class DriverTripsController(
    IDriverTripService driverTripService)
    : ApiControllerBase
{
    [HttpGet("{id:int}")]
    [ProducesResponseType<DriverTripCostResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id, [FromQuery] int? fiscalYearId, CancellationToken cancellationToken)
        => this.ToActionResult(await driverTripService.GetByIdAsync(id, fiscalYearId, cancellationToken));

    [HttpPost]
    [Authorize(Roles = ApplicationRoles.Admin)]
    [ProducesResponseType<DriverTripCostResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(DriverTripRequest request, CancellationToken cancellationToken)
    {
        var result = await driverTripService.AddAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToProblem(result.Errors);
        }

        var trip = result.Value;
        TryEnqueueRealtime<DriverTripsRealtimeJob>("Added", trip.DriverTripId, realtime => job => job.ExecuteAsync(realtime));
        return CreatedAtAction(nameof(GetById), new { id = trip.DriverTripId, fiscalYearId = trip.FiscalYearId }, trip);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = ApplicationRoles.Admin)]
    [ProducesResponseType<DriverTripCostResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, DriverTripUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await driverTripService.UpdateAsync(id, request, cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<DriverTripsRealtimeJob>("Updated", id, realtime => job => job.ExecuteAsync(realtime));
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
            Convert.TryFromBase64String(rowVersion, bytes, out var length) && length == bytes.Length;
        var result = await driverTripService.DeleteAsync(id, decoded ? bytes : null, cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<DriverTripsRealtimeJob>("Deleted", id, realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }

    [HttpGet("cost-entry")]
    [ProducesResponseType<PagedResponse<DriverTripCostResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCostEntry(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] DriverTripCostFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await driverTripService.GetCostEntryAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("bulk-costs")]
    [Authorize(Roles = ApplicationRoles.Admin)]
    [ProducesResponseType<DriverTripBulkCostUpdateResponse>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCosts(
        DriverTripBulkCostUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await driverTripService.UpdateCostsAsync(
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<DriverTripsRealtimeJob>(
                "Updated",
                "bulk",
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }
}
