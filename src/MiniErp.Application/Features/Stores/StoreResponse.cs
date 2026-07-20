namespace MiniErp.Application.Features.Stores;

public sealed record StoreResponse(
    int Id,
    int CompanyId,
    string Code,
    string Name,
    string? Address,
    bool IsActive);
