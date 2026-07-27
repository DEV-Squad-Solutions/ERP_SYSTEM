namespace MiniErp.Application.Features.Containers;

public sealed record ContainerFilterRequest(
    string? Search = null,
    string? Code = null,
    string? Name = null,
    bool? IsActive = null);
