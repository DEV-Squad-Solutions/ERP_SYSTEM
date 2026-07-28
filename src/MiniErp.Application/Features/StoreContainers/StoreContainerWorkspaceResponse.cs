using MiniErp.Application.Features.BusinessPartners;
using MiniErp.Application.Features.Stores;

namespace MiniErp.Application.Features.StoreContainers;

public sealed record StoreContainerWorkspaceResponse(
    StoreResponse ContainerStore,
    BusinessPartnerResponse? BusinessPartner,
    IReadOnlyList<StoreContainerWorkspaceContainerResponse> Containers);
