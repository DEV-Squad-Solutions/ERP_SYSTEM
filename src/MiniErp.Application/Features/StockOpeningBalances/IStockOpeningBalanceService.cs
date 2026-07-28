using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.StockOpeningBalances;

public interface IStockOpeningBalanceService
{
    Task<Result<PagedResponse<StockOpeningBalanceListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        StockOpeningBalanceFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<StockOpeningBalanceResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<StockOpeningBalanceResponse>> AddAsync(
        StockOpeningBalanceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<StockOpeningBalanceResponse>> UpdateAsync(
        int id,
        StockOpeningBalanceUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
