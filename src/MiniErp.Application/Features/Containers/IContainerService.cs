using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Containers;

public interface IContainerService
{
    Task<Result<PagedResponse<ContainerResponse>>> GetAllAsync(
        PaginationRequest pagination,
        ContainerFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<ContainerResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<ContainerResponse>> AddAsync(
        ContainerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ContainerResponse>> UpdateAsync(
        int id,
        ContainerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
