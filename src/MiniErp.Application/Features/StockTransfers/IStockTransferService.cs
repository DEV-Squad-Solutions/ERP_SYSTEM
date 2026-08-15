using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.StockTransfers;

public interface IStockTransferService
{
    Task<Result<PagedResponse<StockTransferListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        StockTransferFilterRequest filters,
        CancellationToken cancellationToken = default);

    Task<Result<StockTransferResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<StockTransferResponse>> AddAsync(
        StockTransferRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<StockTransferResponse>> UpdateAsync(
        int id,
        StockTransferUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
