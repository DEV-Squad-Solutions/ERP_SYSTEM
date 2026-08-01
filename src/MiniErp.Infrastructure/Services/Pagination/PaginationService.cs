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
        if (IsInvalid(pagination))
        {
            return Result<PagedResponse<TResponse>>.Failure(PaginationErrors.Invalid());
        }

        var totalCount = await query.CountAsync(cancellationToken);
        return await CreatePageAsync<TEntity, TResponse>(
            query,
            pagination,
            totalCount,
            cancellationToken);
    }

    public async Task<Result<PagedResponse<TResponse>>> PaginateAsync<TEntity, TResponse>(
        IOrderedQueryable<TEntity> query,
        PaginationRequest pagination,
        int totalCount,
        CancellationToken cancellationToken = default)
    {
        if (IsInvalid(pagination))
        {
            return await PaginateAsync<TEntity, TResponse>(
                query,
                pagination,
                cancellationToken);
        }

        return await CreatePageAsync<TEntity, TResponse>(
            query,
            pagination,
            totalCount,
            cancellationToken);
    }

    private static async Task<Result<PagedResponse<TResponse>>>
        CreatePageAsync<TEntity, TResponse>(
            IOrderedQueryable<TEntity> query,
            PaginationRequest pagination,
            int totalCount,
            CancellationToken cancellationToken)
    {
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

    private static bool IsInvalid(PaginationRequest pagination) =>
        pagination.PageNumber <= 0 ||
        pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize;
}
