using Mapster;
using MiniErp.Domain.Entities.ReferenceData;

namespace MiniErp.Application.Features.Countries;

public sealed class CountryMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<CountryRequest, Country>()
            .Map(country => country.Code, request => request.Code.Trim())
            .Map(country => country.Name, request => request.Name.Trim())
            .Map(
                country => country.ArabicName,
                request => request.ArabicName.Trim());
    }
}
