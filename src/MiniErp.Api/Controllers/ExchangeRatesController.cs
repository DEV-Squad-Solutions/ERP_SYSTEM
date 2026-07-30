using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Enums;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class ExchangeRatesController(
    IExchangeRateService exchangeRateService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<ExchangeRateResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] ExchangeRateFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await exchangeRateService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ExchangeRateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await exchangeRateService.GetByIdAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("resolve")]
    [ProducesResponseType<ExchangeRateResolutionResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Resolve(
        [FromQuery] CurrencyCode currency,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        var result = await exchangeRateService.ResolveAsync(
            currency,
            date,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<ExchangeRateResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        ExchangeRateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await exchangeRateService.AddAsync(
            request,
            cancellationToken);

        return result.IsFailure
            ? this.ToProblem(result.Error)
            : CreatedAtAction(
                nameof(GetById),
                new { id = result.Value.Id },
                result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType<ExchangeRateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        ExchangeRateUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await exchangeRateService.UpdateAsync(
            id,
            request,
            cancellationToken);
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
        var result = await exchangeRateService.DeleteAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }
}
