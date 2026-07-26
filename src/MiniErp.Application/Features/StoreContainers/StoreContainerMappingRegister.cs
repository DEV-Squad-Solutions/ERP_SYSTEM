using Mapster;
using MiniErp.Domain.Entities.Containers;

namespace MiniErp.Application.Features.StoreContainers;

public sealed class StoreContainerMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<StoreContainer, StoreContainerResponse>()
            .Map(response => response.StoreCode, assignment => assignment.Store.Code)
            .Map(response => response.StoreName, assignment => assignment.Store.Name)
            .Map(
                response => response.BusinessPartnerId,
                assignment => assignment.Store.BusinessPartnerId)
            .Map(
                response => response.BusinessPartnerName,
                assignment => assignment.Store.BusinessPartner == null
                    ? null
                    : assignment.Store.BusinessPartner.Name)
            .Map(
                response => response.ContainerCode,
                assignment => assignment.Container.Code)
            .Map(
                response => response.ContainerName,
                assignment => assignment.Container.Name)
            .Map(
                response => response.BusinessPartner,
                assignment => assignment.Store.BusinessPartner);
    }
}
