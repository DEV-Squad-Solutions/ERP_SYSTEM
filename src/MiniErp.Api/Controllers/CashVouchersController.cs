using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.CashVouchers.Jobs;
using MiniErp.Api.Features.ExchangeRates.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.CashVouchers;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class CashVouchersController(
    ICashVoucherService cashVoucherService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<CashVoucherResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] CashVoucherFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await cashVoucherService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<CashVoucherResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await cashVoucherService.GetByIdAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<CashVoucherResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CashVoucherRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cashVoucherService.AddAsync(
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<CashVouchersRealtimeJob>(
                "Added",
                result.Value.Id,
                realtime => job => job.ExecuteAsync(realtime));
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
    [ProducesResponseType<CashVoucherBulkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Bulk(
        CashVoucherBulkRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cashVoucherService.BulkAsync(
            request,
            cancellationToken);
        if (result.IsFailure)
        {
            return this.ToProblem(result.Errors);
        }

        var operationId = Guid.NewGuid();
        foreach (var item in result.Value.Items)
        {
            TryEnqueueRealtime<CashVouchersRealtimeJob>(
                item.Status,
                item.Id,
                realtime => job => job.ExecuteAsync(realtime),
                operationId: operationId);
        }

        for (var index = 0; index < request.Items!.Count; index++)
        {
            var item = request.Items[index];
            var exchangeRate = item switch
            {
                CashVoucherBulkAddItemRequest add => add.Voucher?.ExchangeRate,
                CashVoucherBulkUpdateItemRequest update =>
                    update.Voucher?.ExchangeRate,
                _ => null
            };
            if (!exchangeRate.HasValue)
            {
                continue;
            }

            var response = result.Value.Items[index];
            TryEnqueueRealtime<ExchangeRatesRealtimeJob>(
                "Updated",
                $"{response.Voucher!.Currency}:{response.Voucher.VoucherDate}",
                realtime => job => job.ExecuteAsync(realtime),
                operationId: operationId);
        }

        return Ok(result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType<CashVoucherResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        CashVoucherUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cashVoucherService.UpdateAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            var operationId = Guid.NewGuid();
            TryEnqueueRealtime<CashVouchersRealtimeJob>(
                "Updated",
                id,
                realtime => job => job.ExecuteAsync(realtime),
                operationId: operationId);
            if (request.ExchangeRate.HasValue)
            {
                TryEnqueueRealtime<ExchangeRatesRealtimeJob>(
                    "Updated",
                    $"{result.Value.Currency}:{result.Value.VoucherDate}",
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
        var result = await cashVoucherService.DeleteAsync(
            id,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<CashVouchersRealtimeJob>(
                "Deleted",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }
}
