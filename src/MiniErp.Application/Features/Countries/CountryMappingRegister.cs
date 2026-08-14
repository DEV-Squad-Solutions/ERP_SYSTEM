using Mapster;
using MiniErp.Domain.Entities.ReferenceData;

namespace MiniErp.Application.Features.Countries;

public sealed class CountryMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<CountryRequest, Country>()
            .Ignore(country => country.Code)
            .Map(country => country.Name, request => request.Name.Trim())
            .Map(
                country => country.ArabicName,
                request => request.ArabicName.Trim());
    }
}
