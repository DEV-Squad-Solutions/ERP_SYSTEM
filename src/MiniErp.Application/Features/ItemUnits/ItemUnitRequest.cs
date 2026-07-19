namespace MiniErp.Application.Features.ItemUnits;

public sealed record ItemUnitRequest(
    string Name,
    bool IsActive = true);
