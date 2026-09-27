using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.StockAdjustments.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.StockAdjustments;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class StockAdjustmentsController(
    IStockAdjustmentService stockAdjustmentService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<StockAdjustmentListResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] StockAdjustmentFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await stockAdjustmentService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<StockAdjustmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        [FromQuery] int? fiscalYearId,
        CancellationToken cancellationToken)
    {
        var result = await stockAdjustmentService.GetByIdAsync(
            id,
            fiscalYearId,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<StockAdjustmentResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await stockAdjustmentService.AddAsync(
            request,
            cancellationToken);

        if (result.IsSuccess)
        {
            TryEnqueueRealtime<StockAdjustmentsRealtimeJob>(
                "Added",
                result.Value.Id,
                realtime => job => job.ExecuteAsync(realtime));
        }

        return result.IsFailure
            ? this.ToProblem(result.Errors)
            : CreatedAtAction(
                nameof(GetById),
                new
                {
                    id = result.Value.Id,
                    fiscalYearId = result.Value.FiscalYearId
                },
                result.Value);
    }


    [HttpPut("{id:int}")]
    [ProducesResponseType<StockAdjustmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        StockAdjustmentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await stockAdjustmentService.UpdateAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<StockAdjustmentsRealtimeJob>(
                "Updated",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await stockAdjustmentService.DeleteAsync(
            id,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<StockAdjustmentsRealtimeJob>(
                "Deleted",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }
}
