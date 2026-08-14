namespace MiniErp.Application.Features.ItemsCategories;

public sealed record ItemsCategoryRequest(
    string Name,
    bool IsActive = true,
    string? Notes = null)
{
    public const int NameMaximumLength = 200;

    public const int NotesMaximumLength = 1_000;
}

public sealed record ItemsCategoryUpdateRequest(
    string Name,
    bool IsActive,
    string? Notes,
    byte[]? RowVersion);
