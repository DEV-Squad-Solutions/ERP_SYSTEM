using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.JournalEntries.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.JournalEntries;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class JournalEntriesController(
    IJournalEntryService journalEntryService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<JournalEntryResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] JournalEntryFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await journalEntryService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<JournalEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await journalEntryService.GetByIdAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<JournalEntryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        JournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await journalEntryService.AddAsync(
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<JournalEntriesRealtimeJob>(
                "Posted",
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
    [ProducesResponseType<JournalEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        JournalEntryUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await journalEntryService.UpdateAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<JournalEntriesRealtimeJob>(
                "Updated",
                result.Value.Id,
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
        var result = await journalEntryService.DeleteAsync(
            id,
            rowVersion,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<JournalEntriesRealtimeJob>(
                "Deleted",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }

        return this.ToActionResult(result);
    }

}
