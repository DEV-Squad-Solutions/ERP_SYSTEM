namespace MiniErp.Application.Features.ItemsCategories;

public sealed record ItemsCategoryFilterRequest(
    string? Search = null,
    string? Name = null,
    bool? IsActive = null);
