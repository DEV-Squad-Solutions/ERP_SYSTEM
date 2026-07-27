namespace MiniErp.Application.Features.Companies;

public sealed record CompanyFilterRequest(
    string? Search = null,
    string? Name = null,
    string? Address = null,
    string? CommercialRegister = null,
    string? TaxNumber = null,
    string? ManagerName = null);
