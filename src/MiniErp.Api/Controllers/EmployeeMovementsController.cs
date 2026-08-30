using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.EmployeeMovements.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.EmployeeMovements;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class EmployeeMovementsController(
    IEmployeeMovementService employeeMovementService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<EmployeeMovementResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] EmployeeMovementFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await employeeMovementService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<EmployeeMovementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await employeeMovementService.GetByIdAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<EmployeeMovementResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        EmployeeMovementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await employeeMovementService.AddAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            var operationId = Guid.NewGuid();
            TryEnqueueRealtime<EmployeeMovementsRealtimeJob>(
                "Added",
                result.Value.Id,
                realtime => job => job.ExecuteAsync(realtime),
                operationId: operationId);
        }

        return result.IsFailure
            ? this.ToProblem(result.Errors)
            : CreatedAtAction(
                nameof(GetById),
                new { id = result.Value.Id },
                result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("bulk")]
    [ProducesResponseType<List<EmployeeMovementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateBulk(
        BulkEmployeeMovementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await employeeMovementService.AddBulkAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            foreach (var movement in result.Value)
            {
                TryEnqueueRealtime<EmployeeMovementsRealtimeJob>(
                    "Added",
                    movement.Id,
                    realtime => job => job.ExecuteAsync(realtime));
            }
        }

        return this.ToActionResult(result);
    }
}
