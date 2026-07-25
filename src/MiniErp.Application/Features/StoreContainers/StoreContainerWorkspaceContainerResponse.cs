namespace MiniErp.Application.Features.StoreContainers;

public sealed record StoreContainerWorkspaceContainerResponse(
    int Id,
    int CompanyId,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    bool IsAssigned,
    int? StoreContainerId);
