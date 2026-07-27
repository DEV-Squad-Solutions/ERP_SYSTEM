using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Common.Abstractions;

public interface IPaginationService
{
    Task<Result<PagedResponse<TResponse>>> PaginateAsync<TEntity, TResponse>(
        IOrderedQueryable<TEntity> query,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResponse<TResponse>>> PaginateAsync<TEntity, TResponse>(
        IOrderedQueryable<TEntity> query,
        PaginationRequest pagination,
        int totalCount,
        CancellationToken cancellationToken = default);
}
