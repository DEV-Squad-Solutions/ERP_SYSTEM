namespace MiniErp.Application.Features.Items;

public sealed record ItemResponse(
    int Id,
    int ItemUnitId,
    string ItemUnitName,
    string Code,
    string Name,
    string? Description,
    bool IsActive);
