using Mapster;
using MiniErp.Domain.Entities;

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
                request => request.Description == null
                    ? null
                    : request.Description.Trim());
    }
}