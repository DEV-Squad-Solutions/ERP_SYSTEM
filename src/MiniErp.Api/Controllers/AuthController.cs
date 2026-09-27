using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Features.Authentication;
using static MiniErp.Application.Features.Authentication.AuthenticationErrors;

namespace MiniErp.Api.Controllers;

public sealed class AuthController(
    IAuthenticationService authenticationService)
    : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(
            request,
            cancellationToken);

        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("select-company")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SelectCompany(
        SelectCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.SelectCompanyAsync(
            request,
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("companies")]
    [ProducesResponseType<IReadOnlyList<CompanyAccessResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCompanies(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return this.ToProblem(InvalidUserContext());
        }

        var result = await authenticationService.GetCompaniesAsync(
            userId,
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("switch-company")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SwitchCompany(
        SwitchCompanyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId) ||
            !CompanyClaimResolver.TryGetCompanyId(User, out var companyId))
        {
            return this.ToProblem(InvalidUserContext());
        }

        var result = await authenticationService.SwitchCompanyAsync(
            userId,
            companyId,
            request,
            cancellationToken);

        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RefreshAsync(
            request,
            cancellationToken);

        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LogoutAsync(
            request,
            cancellationToken);

        return this.ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out userId) &&
        userId != Guid.Empty;
}
