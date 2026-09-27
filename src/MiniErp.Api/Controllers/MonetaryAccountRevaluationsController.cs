using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Features.MonetaryAccountRevaluations;
using MiniErp.Domain.Enums;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class MonetaryAccountRevaluationsController(
    IMonetaryAccountRevaluationService service) : ApiControllerBase
{
    [HttpGet("options")]
    [ProducesResponseType<IReadOnlyList<MonetaryAccountRevaluationOption>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOptions(
        [FromQuery] int? fiscalYearId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetOptionsAsync(fiscalYearId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MonetaryAccountRevaluationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] int? accountId,
        [FromQuery] JournalPartyType? partyType,
        [FromQuery] int? partyId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int? fiscalYearId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(
            accountId,
            partyType,
            partyId,
            fromDate,
            toDate,
            fiscalYearId,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<MonetaryAccountRevaluationResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        MonetaryAccountRevaluationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return result.IsFailure
            ? this.ToProblem(result.Errors)
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }
}
