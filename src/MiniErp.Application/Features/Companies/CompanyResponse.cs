namespace MiniErp.Application.Features.Companies;

public sealed record CompanyResponse(
    int Id,
    string Name,
    string Address,
    string CommercialRegister,
    string TaxNumber,
    string ManagerName,
    DateTime CreatedAt);
