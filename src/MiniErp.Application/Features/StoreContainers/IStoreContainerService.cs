using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.StoreContainers;

public interface IStoreContainerService
{
    Task<Result<PagedResponse<StoreContainerResponse>>> GetAllAsync(
        PaginationRequest pagination,
        StoreContainerFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        int storeId,
        CancellationToken cancellationToken = default);

    Task<Result<StoreContainerWorkspaceResponse>> GetWorkspaceAsync(
        int storeId,
        CancellationToken cancellationToken = default);

    Task<Result<StoreContainerResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<StoreContainerResponse>>> UpsertAsync(
        StoreContainerUpsertRequest request,
        CancellationToken cancellationToken = default);
}
