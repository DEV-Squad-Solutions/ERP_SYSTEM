namespace MiniErp.Application.Features.Items;

public sealed record ItemRequest(
    int ItemUnitId,
    string Code,
    string Name,
    string? Description,
    bool IsActive = true);
