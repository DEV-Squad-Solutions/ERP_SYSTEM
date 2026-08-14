using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.Cashboxes.Jobs;
using MiniErp.Api.Features.ExchangeRates.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.Cashboxes;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class CashboxesController(ICashboxService cashboxService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<CashboxResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] CashboxFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await cashboxService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("select")]
    [ProducesResponseType<IReadOnlyList<CashboxSelectResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSelect(
        CancellationToken cancellationToken)
    {
        var result = await cashboxService.GetSelectAsync(cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<CashboxResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await cashboxService.GetByIdAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<CashboxResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CashboxRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cashboxService.AddAsync(
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            var operationId = Guid.NewGuid();
            TryEnqueueRealtime<CashboxesRealtimeJob>(
                "Added",
                result.Value.Id,
                realtime => job => job.ExecuteAsync(realtime),
                operationId: operationId);
            if (request.OpeningExchangeRate.HasValue)
            {
                TryEnqueueRealtime<ExchangeRatesRealtimeJob>(
                    "Updated",
                    $"{result.Value.Currency}:{result.Value.OpeningBalanceDate}",
                    realtime => job => job.ExecuteAsync(realtime),
                    operationId: operationId);
            }
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
    [ProducesResponseType<CashboxResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        CashboxUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cashboxService.UpdateAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            var operationId = Guid.NewGuid();
            TryEnqueueRealtime<CashboxesRealtimeJob>(
                "Updated",
                id,
                realtime => job => job.ExecuteAsync(realtime),
                operationId: operationId);
            if (request.OpeningExchangeRate.HasValue)
            {
                TryEnqueueRealtime<ExchangeRatesRealtimeJob>(
                    "Updated",
                    $"{result.Value.Currency}:{result.Value.OpeningBalanceDate}",
                    realtime => job => job.ExecuteAsync(realtime),
                    operationId: operationId);
            }
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
        var result = await cashboxService.DeleteAsync(
            id,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<CashboxesRealtimeJob>(
                "Deleted",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }
}
