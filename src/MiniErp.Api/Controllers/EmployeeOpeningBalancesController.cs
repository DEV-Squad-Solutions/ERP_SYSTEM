using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.EmployeeOpeningBalances.Jobs;
using MiniErp.Api.Features.ExchangeRates.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.EmployeeOpeningBalances;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class EmployeeOpeningBalancesController(
    IEmployeeOpeningBalanceService employeeOpeningBalanceService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<EmployeeOpeningBalanceResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] EmployeeOpeningBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await employeeOpeningBalanceService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<EmployeeOpeningBalanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await employeeOpeningBalanceService.GetByIdAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<EmployeeOpeningBalanceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        EmployeeOpeningBalanceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await employeeOpeningBalanceService.AddAsync(
            request,
            cancellationToken);

        if (result.IsSuccess)
        {
            var operationId = Guid.NewGuid();
            TryEnqueueRealtime<EmployeeOpeningBalancesRealtimeJob>(
                "Added",
                result.Value.Id,
                realtime => job => job.ExecuteAsync(realtime),
                operationId: operationId);
            if (request.ExchangeRate.HasValue)
            {
                TryEnqueueRealtime<ExchangeRatesRealtimeJob>(
                    "Updated",
                    $"{result.Value.Currency}:{result.Value.DocumentDate}",
                    realtime => job => job.ExecuteAsync(realtime),
                    operationId: operationId);
            }
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
    [ProducesResponseType<EmployeeOpeningBalanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        EmployeeOpeningBalanceUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await employeeOpeningBalanceService.UpdateAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            var operationId = Guid.NewGuid();
            TryEnqueueRealtime<EmployeeOpeningBalancesRealtimeJob>(
                "Updated",
                id,
                realtime => job => job.ExecuteAsync(realtime),
                operationId: operationId);
            if (request.ExchangeRate.HasValue)
            {
                TryEnqueueRealtime<ExchangeRatesRealtimeJob>(
                    "Updated",
                    $"{result.Value.Currency}:{result.Value.DocumentDate}",
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
        var result = await employeeOpeningBalanceService.DeleteAsync(
            id,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<EmployeeOpeningBalancesRealtimeJob>(
                "Deleted",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }
}
