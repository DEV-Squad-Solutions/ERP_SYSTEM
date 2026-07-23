namespace MiniErp.Application.Features.Containers;

public sealed record ContainerRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive = true);
