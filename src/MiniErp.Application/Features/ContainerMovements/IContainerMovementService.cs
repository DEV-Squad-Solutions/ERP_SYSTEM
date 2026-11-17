using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.ContainerMovements;

public interface IContainerMovementService
{
    Task<Result<ContainerMovementResponse>> CreateAsync(
        ContainerMovementRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ContainerMovementResponse>> UpdateAsync(
        int id,
        ContainerMovementUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        byte[]? rowVersion,
        CancellationToken cancellationToken = default);
}
