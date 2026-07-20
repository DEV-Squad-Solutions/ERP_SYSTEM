namespace MiniErp.Application.Features.Items;

public sealed record ItemResponse(
    int Id,
    int CompanyId,
    int ItemUnitId,
    string ItemUnitName,
    string Code,
    string Name,
    string? Description,
    bool IsActive);
