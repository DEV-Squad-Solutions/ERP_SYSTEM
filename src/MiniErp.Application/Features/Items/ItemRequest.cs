namespace MiniErp.Application.Features.Items;

public sealed record ItemRequest(
    int ItemUnitId,
    string Name,
    string? Description,
    bool IsActive = true);
