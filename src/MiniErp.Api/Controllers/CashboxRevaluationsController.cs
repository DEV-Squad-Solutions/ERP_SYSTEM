using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Features.CashboxRevaluations;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class CashboxRevaluationsController(
    ICashboxRevaluationService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CashboxRevaluationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] int? cashboxId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(cashboxId, fromDate, toDate, cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<CashboxRevaluationResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        CashboxRevaluationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return result.IsFailure
            ? this.ToProblem(result.Errors)
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }
}
