using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Cashboxes;

public interface ICashboxService
{
    Task<Result<PagedResponse<CashboxResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CashboxFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CashboxSelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<CashboxResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<CashboxResponse>> AddAsync(
        CashboxRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CashboxResponse>> UpdateAsync(
        int id,
        CashboxUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
