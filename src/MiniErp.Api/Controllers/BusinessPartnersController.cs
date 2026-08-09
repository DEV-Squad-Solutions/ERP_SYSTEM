using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.BusinessPartners.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.BusinessPartners;
using MiniErp.Application.Features.PartnerItemReports;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class BusinessPartnersController(
    IBusinessPartnerService businessPartnerService,
    IPartnerItemReportService partnerItemReportService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<BusinessPartnerResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] BusinessPartnerFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await businessPartnerService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("select")]
    [ProducesResponseType<IReadOnlyList<BusinessPartnerSelectResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSelect(CancellationToken cancellationToken)
    {
        var result = await businessPartnerService.GetSelectAsync(cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<BusinessPartnerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await businessPartnerService.GetByIdAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}/container-store")]
    [ProducesResponseType<BusinessPartnerContainerStoreResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContainerStore(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await businessPartnerService.GetContainerStoreAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("item-report")]
    [ProducesResponseType<PartnerItemReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItemReport(
        [FromQuery] PartnerItemReportFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await partnerItemReportService.GetAsync(
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<BusinessPartnerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        BusinessPartnerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await businessPartnerService.AddAsync(
            request,
            cancellationToken);

        if (result.IsSuccess)
        {
            TryEnqueueRealtime<BusinessPartnersRealtimeJob>(
                "Added",
                result.Value.Id,
                realtime => job => job.ExecuteAsync(realtime));
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
    [ProducesResponseType<BusinessPartnerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        BusinessPartnerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await businessPartnerService.UpdateAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<BusinessPartnersRealtimeJob>(
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
        var result = await businessPartnerService.DeleteAsync(
            id,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<BusinessPartnersRealtimeJob>(
                "Deleted",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }
}
