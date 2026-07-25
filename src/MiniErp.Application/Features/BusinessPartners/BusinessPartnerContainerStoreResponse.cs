using MiniErp.Application.Features.Stores;
using MiniErp.Application.Features.StoreContainers;

namespace MiniErp.Application.Features.BusinessPartners;

public sealed record BusinessPartnerContainerStoreResponse(
    BusinessPartnerResponse BusinessPartner,
    StoreResponse? ContainerStore,
    IReadOnlyList<StoreContainerWorkspaceContainerResponse> Containers);
