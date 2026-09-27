namespace MiniErp.Application.Features.Authentication;

public sealed record SwitchCompanyRequest(
    int CompanyId,
    string RefreshToken);
