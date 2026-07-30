using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.InventoryCostReports;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class InventoryCostReportsController(
    IInventoryCostReportService reportService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<InventoryCostReportResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Get(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] InventoryCostReportFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await reportService.GetAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }
}
