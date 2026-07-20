namespace MiniErp.Application.Features.Authentication;

public sealed record SelectCompanyRequest(
    string SelectionToken,
    int CompanyId);
