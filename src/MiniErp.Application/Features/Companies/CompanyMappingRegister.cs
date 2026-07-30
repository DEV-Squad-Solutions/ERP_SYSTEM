using Mapster;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Application.Features.Companies;

public sealed class CompanyMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<CompanyRequest, Company>()
            .Ignore(company => company.Settings)
            .Map(company => company.Name, request => request.Name.Trim())
            .Map(company => company.Address, request => request.Address.Trim())
            .Map(
                company => company.CommercialRegister,
                request => request.CommercialRegister.Trim())
            .Map(company => company.TaxNumber, request => request.TaxNumber.Trim())
            .Map(company => company.ManagerName, request => request.ManagerName.Trim());

        config.ForType<Company, CompanyResponse>()
            .Map(response => response.CreatedAt, company => company.CreatedOn)
            .Map(
                response => response.StockBalanceCheckMode,
                company => company.Settings == null
                    ? MiniErp.Domain.Enums.StockBalanceCheckMode.DateCheck
                    : company.Settings.StockBalanceCheckMode)
            .Map(
                response => response.BaseCurrency,
                company => company.Settings == null
                    ? MiniErp.Domain.Enums.CurrencyCode.EGP
                    : company.Settings.BaseCurrency);
    }
}
