using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.FiscalYears.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.FiscalYears;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class FiscalYearsController(IFiscalYearService fiscalYearService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<FiscalYearResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] FiscalYearFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await fiscalYearService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("select")]
    [ProducesResponseType<IReadOnlyList<FiscalYearSelectResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSelect(
        CancellationToken cancellationToken)
    {
        var result = await fiscalYearService.GetSelectAsync(cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("current")]
    [ProducesResponseType<FiscalYearResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrent(
        CancellationToken cancellationToken)
    {
        var result = await fiscalYearService.GetCurrentAsync(cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<FiscalYearResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await fiscalYearService.GetByIdAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<FiscalYearResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        FiscalYearRequest request,
        CancellationToken cancellationToken)
    {
        var result = await fiscalYearService.AddAsync(
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<FiscalYearsRealtimeJob>(
                "Added",
                result.Value.Id,
                realtime => job => job.ExecuteAsync(realtime));
        }

        return result.IsFailure
            ? this.ToProblem(result.Errors)
            : CreatedAtAction(
                nameof(GetById),
                new { id = result.Value.Id },
                result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType<FiscalYearResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        FiscalYearUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await fiscalYearService.UpdateAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<FiscalYearsRealtimeJob>(
                "Updated",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }

        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/close")]
    [ProducesResponseType<FiscalYearResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await fiscalYearService.CloseAsync(
            id,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<FiscalYearsRealtimeJob>(
                "Closed",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }

        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/reopen")]
    [ProducesResponseType<FiscalYearResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reopen(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await fiscalYearService.ReopenAsync(
            id,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<FiscalYearsRealtimeJob>(
                "Reopened",
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
        CancellationToken cancellationToken)
    {
        var result = await fiscalYearService.DeleteAsync(
            id,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<FiscalYearsRealtimeJob>(
                "Deleted",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }

        return this.ToActionResult(result);
    }
}
