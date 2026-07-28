using Mapster;
using MiniErp.Domain.Entities.Catalog;

namespace MiniErp.Application.Features.ItemUnits;

public sealed class ItemUnitMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<ItemUnitRequest, ItemUnit>()
            .Map(itemUnit => itemUnit.Name, request => request.Name.Trim());
    }
}
