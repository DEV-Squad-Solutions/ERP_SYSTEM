using Mapster;
using MiniErp.Domain.Entities.BusinessPartners;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public sealed class PartnerOpeningBalanceMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<PartnerOpeningBalanceRequest, PartnerOpeningBalance>()
            .Ignore(balance => balance.DocumentNumber)
            .Ignore(balance => balance.ExchangeRateRecord)
            .Ignore(balance => balance.ExchangeRateId)
            .Ignore(balance => balance.ExchangeRate)
            .Ignore(balance => balance.BaseAmount)
            .Map(balance => balance.Notes, request => Normalize(request.Notes));

        config.ForType<PartnerOpeningBalanceUpdateRequest, PartnerOpeningBalance>()
            .Ignore(balance => balance.DocumentNumber)
            .Ignore(balance => balance.RowVersion)
            .Ignore(balance => balance.ExchangeRateRecord)
            .Ignore(balance => balance.ExchangeRateId)
            .Ignore(balance => balance.ExchangeRate)
            .Ignore(balance => balance.BaseAmount)
            .Map(balance => balance.Notes, request => Normalize(request.Notes));

        config.ForType<PartnerOpeningBalance, PartnerOpeningBalanceResponse>()
            .Map(
                response => response.BaseCurrency,
                balance => balance.Company.Settings == null
                    ? Domain.Enums.CurrencyCode.EGP
                    : balance.Company.Settings.BaseCurrency)
            .Map(
                response => response.BusinessPartnerName,
                balance => balance.BusinessPartner.Name);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
