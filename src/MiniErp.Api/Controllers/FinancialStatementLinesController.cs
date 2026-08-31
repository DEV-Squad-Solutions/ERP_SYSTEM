using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.FinancialStatementLines.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.FinancialStatementLines;
using MiniErp.Domain.Enums;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class FinancialStatementLinesController(
    IFinancialStatementLineService financialStatementLineService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<FinancialStatementLineResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] FinancialStatementLineFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await financialStatementLineService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("tree")]
    [ProducesResponseType<IReadOnlyList<FinancialStatementLineTreeResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(
        [FromQuery] int fiscalYearId,
        [FromQuery] FinancialStatementType statementType,
        CancellationToken cancellationToken)
    {
        var result = await financialStatementLineService.GetTreeAsync(
            fiscalYearId,
            statementType,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("select")]
    [ProducesResponseType<IReadOnlyList<FinancialStatementLineSelectResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSelect(
        [FromQuery] int fiscalYearId,
        [FromQuery] FinancialStatementType statementType,
        CancellationToken cancellationToken)
    {
        var result = await financialStatementLineService.GetSelectAsync(
            fiscalYearId,
            statementType,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<FinancialStatementLineResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await financialStatementLineService.GetByIdAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<FinancialStatementLineResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        FinancialStatementLineRequest request,
        CancellationToken cancellationToken)
    {
        var result = await financialStatementLineService.AddAsync(
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<FinancialStatementLinesRealtimeJob>(
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
    [ProducesResponseType<FinancialStatementLineResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        FinancialStatementLineUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await financialStatementLineService.UpdateAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<FinancialStatementLinesRealtimeJob>(
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
        CancellationToken cancellationToken)
    {
        var result = await financialStatementLineService.DeleteAsync(
            id,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<FinancialStatementLinesRealtimeJob>(
                "Deleted",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }

        return this.ToActionResult(result);
    }
}
