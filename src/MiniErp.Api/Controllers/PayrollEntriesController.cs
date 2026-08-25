using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.PayrollEntries;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class PayrollEntriesController(
    IPayrollEntryService payrollEntryService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<PayrollEntriesListResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] PayrollEntryFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await payrollEntryService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<PayrollEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await payrollEntryService.GetByIdAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<PayrollEntryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        PayrollEntryCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await payrollEntryService.AddAsync(request, cancellationToken);

        return result.IsFailure
            ? this.ToProblem(result.Errors)
            : CreatedAtAction(
                nameof(GetById),
                new { id = result.Value.Id },
                result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("bulk")]
    [ProducesResponseType<List<PayrollEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateBulk(
        BulkPayrollEntryCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await payrollEntryService.AddBulkAsync(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/move-salary")]
    [ProducesResponseType<PayrollEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MoveSalary(
        int id,
        PayrollEntrySalaryPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await payrollEntryService.MoveSalaryForEmployeeAccountAsync(
            id,
            request,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("bulk/move-salary")]
    [ProducesResponseType<List<PayrollEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MoveSalaryBulk(
        BulkPayrollEntrySalaryPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await payrollEntryService.MoveSalaryForEmployeeAccountBulkAsync(
            request,
            cancellationToken);
        return this.ToActionResult(result);
    }


    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType<PayrollEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        PayrollEntryUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await payrollEntryService.UpdateAsync(id, request, cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/recalculate")]
    [ProducesResponseType<PayrollEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Recalculate(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await payrollEntryService.RecalculateAsync(id, cancellationToken);
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
        var result = await payrollEntryService.DeleteAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }
}
