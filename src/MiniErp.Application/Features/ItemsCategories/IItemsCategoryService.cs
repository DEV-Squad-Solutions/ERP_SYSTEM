using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.ItemsCategories;

public interface IItemsCategoryService
{
    Task<Result<PagedResponse<ItemsCategoryResponse>>> GetAllAsync(
        PaginationRequest pagination,
        ItemsCategoryFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ItemsCategorySelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<ItemsCategoryResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<ItemsCategoryResponse>> AddAsync(
        ItemsCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ItemsCategoryResponse>> UpdateAsync(
        int id,
        ItemsCategoryUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
