namespace MiniErp.Application.Features.StoreContainers;

public sealed record StoreContainerUpsertRequest(
    int StoreId,
    IReadOnlyList<int> ContainerIds)
{
    public const int MaximumContainerCount = 100;
}
