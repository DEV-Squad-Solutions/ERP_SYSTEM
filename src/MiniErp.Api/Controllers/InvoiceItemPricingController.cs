using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.InvoiceItemPricing;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class InvoiceItemPricingController(
    IInvoiceItemPricingService pricingService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<InvoiceItemPricingPagedResponse>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] InvoiceItemPricingFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await pricingService.GetAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    /// <summary>Replaces the advisory per-unit expenses of an item.</summary>
    /// <remarks>
    /// The amounts are pricing hints only. They do not update inventory cost,
    /// inventory valuation, invoices, or accounting entries.
    /// </remarks>
    [HttpPut("items/{itemId:int}/expenses")]
    [ProducesResponseType<ItemPricingExpensesResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceExpenses(
        int itemId,
        ReplaceItemPricingExpensesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await pricingService.ReplaceExpensesAsync(
            itemId,
            request,
            cancellationToken);
        return this.ToActionResult(result);
    }
}
