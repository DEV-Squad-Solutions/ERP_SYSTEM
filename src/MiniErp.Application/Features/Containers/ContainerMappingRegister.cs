using Mapster;
using MiniErp.Domain.Entities.Containers;

namespace MiniErp.Application.Features.Containers;

public sealed class ContainerMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<ContainerRequest, Container>()
            .Map(container => container.Code, request => request.Code.Trim())
            .Map(container => container.Name, request => request.Name.Trim())
            .Map(
                container => container.Description,
                request => string.IsNullOrWhiteSpace(request.Description)
                    ? null
                    : request.Description.Trim());
    }
}
