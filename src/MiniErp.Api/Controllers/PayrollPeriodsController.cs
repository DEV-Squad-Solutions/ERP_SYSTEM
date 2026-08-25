using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.PayrollPeriods;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class PayrollPeriodsController(
    IPayrollPeriodService payrollPeriodService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<PayrollPeriodListResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] PayrollPeriodFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await payrollPeriodService.GetAllAsync(pagination, filters, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<PayrollPeriodResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await payrollPeriodService.GetByIdAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<PayrollPeriodResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        PayrollPeriodCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await payrollPeriodService.CreateAsync(request, cancellationToken);
        return result.IsFailure
            ? this.ToProblem(result.Errors)
            : CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType<PayrollPeriodResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        PayrollPeriodUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await payrollPeriodService.UpdateAsync(id, request, cancellationToken);
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
        var result = await payrollPeriodService.DeleteAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/calculate")]
    [ProducesResponseType<PayrollPeriodResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CalculatePeriod(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await payrollPeriodService.CalculatePeriodAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}/report")]
    [ProducesResponseType<PayrollPeriodReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPeriodReport(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await payrollPeriodService.GetReportByPeriodAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("report")]
    [ProducesResponseType<PayrollPeriodReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDateRangeReport(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var result = await payrollPeriodService.GetReportByDateRangeAsync(startDate, endDate, cancellationToken);
        return this.ToActionResult(result);
    }
}
