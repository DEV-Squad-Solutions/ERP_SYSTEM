namespace MiniErp.Application.Features.StoreContainers;

public sealed record StoreContainerFilterRequest(
    int? StoreId = null,
    int? ContainerId = null,
    int? BusinessPartnerId = null,
    bool? IsActive = null);
