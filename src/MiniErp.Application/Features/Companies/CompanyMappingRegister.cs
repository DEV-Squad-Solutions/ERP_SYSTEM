using Mapster;
using MiniErp.Domain.Entities;

namespace MiniErp.Application.Features.Companies;

public sealed class CompanyMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<CompanyRequest, Company>()
            .Map(company => company.Name, request => request.Name.Trim())
            .Map(company => company.Address, request => request.Address.Trim())
            .Map(
                company => company.CommercialRegister,
                request => request.CommercialRegister.Trim())
            .Map(company => company.TaxNumber, request => request.TaxNumber.Trim())
            .Map(company => company.ManagerName, request => request.ManagerName.Trim());

        config.ForType<Company, CompanyResponse>()
            .Map(response => response.CreatedAt, company => company.CreatedOn);
    }
}
