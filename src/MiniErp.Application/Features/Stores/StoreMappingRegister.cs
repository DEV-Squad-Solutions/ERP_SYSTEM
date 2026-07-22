using Mapster;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Application.Features.Stores;

public sealed class StoreMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<StoreRequest, Store>()
            .Map(store => store.Code, request => request.Code.Trim())
            .Map(store => store.Name, request => request.Name.Trim())
            .Map(
                store => store.Address,
                request => string.IsNullOrWhiteSpace(request.Address)
                    ? null
                    : request.Address.Trim());

        config.ForType<Store, StoreResponse>()
            .Map(
                response => response.BusinessPartnerName,
                store => store.BusinessPartner == null
                    ? null
                    : store.BusinessPartner.Name);
    }
}
