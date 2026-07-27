namespace MiniErp.Application.Features.Stores;

public sealed record StoreFilterRequest(
    string? Search = null,
    string? Code = null,
    string? Name = null,
    int? BusinessPartnerId = null,
    bool? IsContainerStore = null,
    bool? IsActive = null);
