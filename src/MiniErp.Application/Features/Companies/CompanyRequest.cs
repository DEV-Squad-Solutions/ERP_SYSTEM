namespace MiniErp.Application.Features.Companies;

public sealed record CompanyRequest(
    string Name,
    string Address,
    string CommercialRegister,
    string TaxNumber,
    string ManagerName);
