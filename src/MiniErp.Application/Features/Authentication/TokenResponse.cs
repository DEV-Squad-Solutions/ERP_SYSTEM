namespace MiniErp.Application.Features.Authentication;

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken);
