namespace MiniErp.Application.Features.Authentication;

public sealed record LoginRequest(
    string UserName,
    string Password);
