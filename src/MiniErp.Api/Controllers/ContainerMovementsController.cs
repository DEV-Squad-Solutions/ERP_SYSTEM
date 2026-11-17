using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.ContainerMovements.Jobs;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Features.ContainerMovements;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class ContainerMovementsController(
    IContainerMovementService containerMovementService)
    : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = ApplicationRoles.AdminOrAccountant)]
    [ProducesResponseType<ContainerMovementResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        ContainerMovementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await containerMovementService.CreateAsync(
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<ContainerMovementsRealtimeJob>(
                "Created",
                result.Value.Id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return result.IsFailure
            ? this.ToProblem(result.Errors)
            : StatusCode(
                StatusCodes.Status201Created,
                result.Value);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = ApplicationRoles.AdminOrAccountant)]
    [ProducesResponseType<ContainerMovementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        ContainerMovementUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await containerMovementService.UpdateAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<ContainerMovementsRealtimeJob>(
                "Updated",
                result.Value.Id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = ApplicationRoles.AdminOrAccountant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        int id,
        [FromQuery] byte[]? rowVersion,
        CancellationToken cancellationToken)
    {
        var result = await containerMovementService.DeleteAsync(
            id,
            rowVersion,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<ContainerMovementsRealtimeJob>(
                "Deleted",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }
}
