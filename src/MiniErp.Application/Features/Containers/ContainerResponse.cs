namespace MiniErp.Application.Features.Containers;

public sealed record ContainerResponse(
    int Id,
    int CompanyId,
    string Code,
    string Name,
    string? Description,
    bool IsActive);
