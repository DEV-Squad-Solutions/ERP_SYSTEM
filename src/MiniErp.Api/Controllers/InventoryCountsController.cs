using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.InventoryCounts.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.InventoryCounts;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class InventoryCountsController(
    IInventoryCountService inventoryCountService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<InventoryCountListResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] InventoryCountFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await inventoryCountService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<InventoryCountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await inventoryCountService.GetByIdAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<InventoryCountResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        InventoryCountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await inventoryCountService.AddAsync(
            request,
            cancellationToken);

        if (result.IsSuccess)
        {
            TryEnqueueRealtime<InventoryCountsRealtimeJob>(
                "Added",
                result.Value.Id,
                realtime => job => job.ExecuteAsync(realtime));
        }

        return result.IsFailure
            ? this.ToProblem(result.Error)
            : CreatedAtAction(
                nameof(GetById),
                new { id = result.Value.Id },
                result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType<InventoryCountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        InventoryCountUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await inventoryCountService.UpdateAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<InventoryCountsRealtimeJob>(
                "Updated",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/reconcile")]
    [ProducesResponseType<InventoryCountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reconcile(
        int id,
        InventoryCountReconcileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await inventoryCountService.ReconcileAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<InventoryCountsRealtimeJob>(
                "Updated",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        int id,
        [FromQuery] byte[]? rowVersion,
        CancellationToken cancellationToken)
    {
        var result = await inventoryCountService.DeleteAsync(
            id,
            rowVersion,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<InventoryCountsRealtimeJob>(
                "Deleted",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }
}
