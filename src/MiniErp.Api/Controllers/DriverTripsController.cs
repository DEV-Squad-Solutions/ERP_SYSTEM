using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.DriverTrips.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.DriverTrips;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class DriverTripsController(
    IDriverTripService driverTripService)
    : ApiControllerBase
{
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

    [Authorize(Roles = "Admin")]
    [HttpPut("bulk-costs")]
    [ProducesResponseType<DriverTripBulkCostUpdateResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
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
