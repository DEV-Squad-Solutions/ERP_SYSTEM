using System.Text.Json.Serialization;
using MiniErp.Application.Features.Stores;
using MiniErp.Application.Features.StoreContainers;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.BusinessPartners;

public sealed record BusinessPartnerResponse(
    int Id,
    int CompanyId,
    string Code,
    string Name,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? TaxNumber,
    CurrencyCode Currency,
    decimal CreditLimit,
    bool IsActive,
    bool Special)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StoreResponse? ContainerStore { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<StoreContainerWorkspaceContainerResponse>? Containers
    {
        get;
        init;
    }
}
