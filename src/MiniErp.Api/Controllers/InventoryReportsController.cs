using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.InventoryCostReports;
using MiniErp.Application.Features.InventoryStockReports;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class InventoryReportsController(
    IInventoryCostReportService costReportService,
    IInventoryStockReportService stockReportService)
    : ApiControllerBase
{
    [HttpGet("cost")]
    [ProducesResponseType<InventoryCostReportResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetCostReport(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] InventoryCostReportFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await costReportService.GetAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("stock")]
    [ProducesResponseType<InventoryStockReportResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStockReport(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] InventoryStockReportFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await stockReportService.GetAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }
}
