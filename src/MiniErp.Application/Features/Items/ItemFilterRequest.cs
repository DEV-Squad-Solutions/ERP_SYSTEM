namespace MiniErp.Application.Features.Items;

public sealed record ItemFilterRequest(
    string? Search = null,
    string? Code = null,
    string? Name = null,
    int? ItemUnitId = null,
    bool? IsActive = null);
