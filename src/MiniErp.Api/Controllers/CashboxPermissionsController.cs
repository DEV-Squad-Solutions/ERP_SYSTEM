using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;

namespace MiniErp.Api.Controllers;

public sealed class CashboxPermissionsController(ICashboxAccessService cashboxAccessService) : ApiControllerBase
{
    [HttpGet("current")]
    [ProducesResponseType<CashboxAccessScope>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken) =>
        this.ToActionResult(await cashboxAccessService.GetScopeAsync(cancellationToken));
}
