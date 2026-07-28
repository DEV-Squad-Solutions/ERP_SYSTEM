using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.CashMovementTypes;

public interface ICashMovementTypeService
{
    Task<Result<PagedResponse<CashMovementTypeResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CashMovementTypeFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CashMovementTypeSelectResponse>>> GetSelectAsync(
        CashMovementTypeSelectRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<CashMovementTypeResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<CashMovementTypeResponse>> AddAsync(
        CashMovementTypeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CashMovementTypeResponse>> UpdateAsync(
        int id,
        CashMovementTypeUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
