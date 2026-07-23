namespace MiniErp.Application.Features.StoreContainers;

public sealed record StoreContainerResponse(
    int Id,
    int CompanyId,
    int StoreId,
    string StoreCode,
    string StoreName,
    int? BusinessPartnerId,
    string? BusinessPartnerName,
    int ContainerId,
    string ContainerCode,
    string ContainerName,
    bool IsActive);
