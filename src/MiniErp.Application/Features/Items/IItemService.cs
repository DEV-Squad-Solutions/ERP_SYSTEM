using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Items;

public interface IItemService
{
    Task<Result<PagedResponse<ItemResponse>>> GetAllAsync(
        PaginationRequest pagination,
        ItemFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<ItemResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<ItemResponse>> AddAsync(
        ItemRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ItemResponse>> UpdateAsync(
        int id,
        ItemRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
