namespace MiniErp.Application.Features.Stores;

public sealed record StoreRequest(
    string Code,
    string Name,
    string? Address,
    bool IsActive = true);
