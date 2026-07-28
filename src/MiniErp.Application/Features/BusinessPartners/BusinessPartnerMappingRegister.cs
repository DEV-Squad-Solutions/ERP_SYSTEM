using Mapster;
using MiniErp.Domain.Entities.BusinessPartners;

namespace MiniErp.Application.Features.BusinessPartners;

public sealed class BusinessPartnerMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<BusinessPartnerRequest, BusinessPartner>()
            .Map(partner => partner.Code, request => request.Code.Trim())
            .Map(partner => partner.Name, request => request.Name.Trim())
            .Map(
                partner => partner.PhoneNumber,
                request => string.IsNullOrWhiteSpace(request.PhoneNumber)
                    ? null
                    : request.PhoneNumber.Trim())
            .Map(
                partner => partner.Email,
                request => string.IsNullOrWhiteSpace(request.Email)
                    ? null
                    : request.Email.Trim())
            .Map(
                partner => partner.Address,
                request => string.IsNullOrWhiteSpace(request.Address)
                    ? null
                    : request.Address.Trim())
            .Map(
                partner => partner.TaxNumber,
                request => string.IsNullOrWhiteSpace(request.TaxNumber)
                    ? null
                    : request.TaxNumber.Trim());
    }
}
