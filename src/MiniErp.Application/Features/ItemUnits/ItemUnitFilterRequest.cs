namespace MiniErp.Application.Features.ItemUnits;

public sealed record ItemUnitFilterRequest(
    string? Search = null,
    string? Name = null,
    bool? IsActive = null);
