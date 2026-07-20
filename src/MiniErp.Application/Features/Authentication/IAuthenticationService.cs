using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Authentication;

public interface IAuthenticationService
{
    Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TokenResponse>> SelectCompanyAsync(
        SelectCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TokenResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> LogoutAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);
}
