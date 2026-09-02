namespace MiniErp.Application.Features.Authentication;

public sealed record TokenResponse(
    Guid UserId,
    string AccessToken,
    string RefreshToken);
