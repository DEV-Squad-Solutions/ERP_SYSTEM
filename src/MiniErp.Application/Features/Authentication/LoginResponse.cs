namespace MiniErp.Application.Features.Authentication;

public sealed record LoginResponse(
    Guid UserId,
    bool RequiresCompanySelection,
    string? SelectionToken,
    string? AccessToken,
    string? RefreshToken,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<CompanyAccessResponse> Companies);
