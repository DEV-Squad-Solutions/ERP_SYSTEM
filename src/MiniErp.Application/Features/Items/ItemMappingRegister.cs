using Mapster;
using MiniErp.Domain.Entities.Catalog;

namespace MiniErp.Application.Features.Items;

public sealed class ItemMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<ItemRequest, Item>()
            .Map(item => item.Code, request => request.Code.Trim())
            .Map(item => item.Name, request => request.Name.Trim())
            .Map(
                item => item.Description,
                request => string.IsNullOrWhiteSpace(request.Description)
                    ? null
                    : request.Description.Trim());
    }
}
