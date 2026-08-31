using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.JournalEntries.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.JournalEntries;

namespace MiniErp.Api.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
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
    [HttpPost("{id:int}/reverse")]
    [ProducesResponseType<JournalEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reverse(
        int id,
        JournalEntryReverseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await journalEntryService.ReverseAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<JournalEntriesRealtimeJob>(
                "Reversed",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }

        return this.ToActionResult(result);
    }
}
