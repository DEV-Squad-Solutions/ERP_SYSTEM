namespace MiniErp.Application.Features.Containers;

public sealed record ContainerRequest(
    string Name,
    string? Description,
    bool IsActive = true);
