namespace MiniErp.Application.Features.ItemsCategories;

public sealed record ItemsCategoryResponse(
    int Id,
    int CompanyId,
    string Name,
    bool IsActive,
    string? Notes,
    byte[] RowVersion);

public sealed record ItemsCategorySelectResponse(
    int Id,
    string Name);
