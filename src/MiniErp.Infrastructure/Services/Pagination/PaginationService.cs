using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Infrastructure.Services.Pagination;

public sealed class PaginationService : IPaginationService, IScopedService
{
    public async Task<Result<PagedResponse<TResponse>>> PaginateAsync<TEntity, TResponse>(
        IOrderedQueryable<TEntity> query,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        if (pagination.PageNumber <= 0 ||
            pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
        {
            return Result<PagedResponse<TResponse>>.Failure(
                Error.Validation(
                    "Pagination.Invalid",
                    $"Page number must be greater than zero and page size must be between 1 and {PaginationRequest.MaxPageSize}."));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        IReadOnlyList<TResponse> items = offset >= totalCount
            ? []
            : await query
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .ProjectToType<TResponse>()
                .ToListAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pagination.PageSize);

        return Result<PagedResponse<TResponse>>.Success(
            new PagedResponse<TResponse>(
                items,
                pagination.PageNumber,
                pagination.PageSize,
                totalCount,
                totalPages));
    }
}
