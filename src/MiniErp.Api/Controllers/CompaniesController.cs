using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.Companies.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.Companies;

namespace MiniErp.Api.Controllers;

[Authorize(Roles = "Admin")]
public sealed class CompaniesController(ICompanyService companyService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<CompanyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] CompanyFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await companyService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("select")]
    [ProducesResponseType<IReadOnlyList<SelectResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSelect(CancellationToken cancellationToken)
    {
        var result = await companyService.GetSelectAsync(cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await companyService.GetByIdAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CompanyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await companyService.AddAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            TryEnqueueRealtime<CompaniesRealtimeJob>(
                "Added",
                result.Value.Id,
                realtime => job => job.ExecuteAsync(realtime),
                companyId: result.Value.Id);
        }

        return result.IsFailure
            ? this.ToProblem(result.Error)
            : CreatedAtAction(
                nameof(GetById),
                new { id = result.Value.Id },
                result.Value);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        CompanyUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await companyService.UpdateAsync(
            id,
            request,
            cancellationToken);

        if (result.IsSuccess)
        {
            TryEnqueueRealtime<CompaniesRealtimeJob>(
                "Updated",
                id,
                realtime => job => job.ExecuteAsync(realtime),
                companyId: id);
        }

        return this.ToActionResult(result);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await companyService.DeleteAsync(id, cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<CompaniesRealtimeJob>(
                "Deleted",
                id,
                realtime => job => job.ExecuteAsync(realtime),
                companyId: id);
        }
        return this.ToActionResult(result);
    }
}
