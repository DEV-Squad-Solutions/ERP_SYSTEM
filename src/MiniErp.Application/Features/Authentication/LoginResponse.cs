namespace MiniErp.Application.Features.Authentication;

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    string FullName,
    string Email);
