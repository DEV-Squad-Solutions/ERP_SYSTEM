using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Features.Users;

namespace MiniErp.Api.Controllers;

[Authorize(Roles = ApplicationRoles.Admin)]
[Route("api/v{version:apiVersion}/Users/{userId:guid}/cashbox-permissions")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class UserCashboxPermissionsController(IUserCashboxPermissionService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<UserCashboxPermissionsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.GetAsync(userId, cancellationToken));

    [HttpPut]
    [ProducesResponseType<UserCashboxPermissionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Replace(Guid userId, UserCashboxPermissionsRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.ReplaceAsync(userId, request, cancellationToken));
}
