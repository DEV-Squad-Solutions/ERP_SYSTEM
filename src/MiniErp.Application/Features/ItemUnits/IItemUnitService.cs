using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.ItemUnits;

public interface IItemUnitService
{
    Task<Result<PagedResponse<ItemUnitResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<ItemUnitResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<ItemUnitResponse>> AddAsync(
        ItemUnitRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ItemUnitResponse>> UpdateAsync(
        int id,
        ItemUnitRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
