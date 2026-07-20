using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Stores;

public interface IStoreService
{
    Task<Result<PagedResponse<StoreResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<StoreResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<StoreResponse>> AddAsync(
        StoreRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<StoreResponse>> UpdateAsync(
        int id,
        StoreRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
