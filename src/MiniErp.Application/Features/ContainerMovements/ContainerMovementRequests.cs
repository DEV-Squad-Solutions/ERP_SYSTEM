namespace MiniErp.Application.Features.ContainerMovements;

public sealed record ContainerMovementRequest(
    int ContainerStoreId,
    int ContainerId,
    DateOnly MovementDate,
    int OutgoingUnits,
    int IncomingUnits,
    string? Description)
{
    public const int DescriptionMaximumLength = 1_000;
}

public sealed record ContainerMovementUpdateRequest(
    int ContainerId,
    DateOnly MovementDate,
    int OutgoingUnits,
    int IncomingUnits,
    string? Description,
    byte[]? RowVersion);
