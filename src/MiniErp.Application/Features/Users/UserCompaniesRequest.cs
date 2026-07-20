namespace MiniErp.Application.Features.Users;

public sealed record UserCompaniesRequest(
    IReadOnlyCollection<int> CompanyIds);
