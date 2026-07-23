using System.ComponentModel.DataAnnotations;

namespace MiniErp.Application.Features.StoreContainers;

public sealed record StoreContainerUpsertRequest(
    [property: Required]
    int StoreId,
    [property: Required]
    [property: MaxLength(100)]
    IReadOnlyList<int> ContainerIds)
{
    public const int MaximumContainerCount = 100;
}
