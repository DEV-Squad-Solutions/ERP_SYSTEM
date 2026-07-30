using Mapster;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Application.Features.ExchangeRates;

public sealed class ExchangeRateMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<ExchangeRate, ExchangeRateResponse>()
            .Map(
                response => response.BaseCurrency,
                rate => rate.Company.Settings == null
                    ? Domain.Enums.CurrencyCode.EGP
                    : rate.Company.Settings.BaseCurrency)
            .Map(response => response.Id, rate => rate.Id)
            .Map(response => response.CompanyId, rate => rate.CompanyId)
            .Map(response => response.Currency, rate => rate.Currency)
            .Map(response => response.RateDate, rate => rate.RateDate)
            .Map(response => response.Rate, rate => rate.Rate)
            .Map(response => response.Source, rate => rate.Source)
            .Map(response => response.Notes, rate => rate.Notes)
            .Map(response => response.RowVersion, rate => rate.RowVersion);
    }
}
