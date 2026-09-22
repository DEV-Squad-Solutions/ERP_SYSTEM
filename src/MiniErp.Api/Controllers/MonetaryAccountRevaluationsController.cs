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
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
    {
        var result = await service.GetOptionsAsync(cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(accountId, partyType, partyId, fromDate, toDate, cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
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
