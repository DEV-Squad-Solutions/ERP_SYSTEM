using Mapster;
using MiniErp.Domain.Entities.BusinessPartners;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public sealed class PartnerOpeningBalanceMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<PartnerOpeningBalanceRequest, PartnerOpeningBalance>()
            .Map(
                balance => balance.DocumentNumber,
                request => request.DocumentNumber.Trim())
            .Map(balance => balance.Notes, request => Normalize(request.Notes));

        config.ForType<PartnerOpeningBalanceUpdateRequest, PartnerOpeningBalance>()
            .Ignore(balance => balance.RowVersion)
            .Map(
                balance => balance.DocumentNumber,
                request => request.DocumentNumber.Trim())
            .Map(balance => balance.Notes, request => Normalize(request.Notes));

        config.ForType<PartnerOpeningBalance, PartnerOpeningBalanceResponse>()
            .Map(
                response => response.BusinessPartnerName,
                balance => balance.BusinessPartner.Name);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
