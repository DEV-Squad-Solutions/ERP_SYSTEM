namespace MiniErp.Application.Features.ItemUnits;

public sealed record ItemUnitResponse(
    int Id,
    int CompanyId,
    string Name,
    bool IsActive);
