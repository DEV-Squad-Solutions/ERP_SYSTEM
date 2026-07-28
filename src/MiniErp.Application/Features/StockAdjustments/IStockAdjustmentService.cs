using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.StockAdjustments;

public interface IStockAdjustmentService
{
    Task<Result<PagedResponse<StockAdjustmentListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        StockAdjustmentFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<StockAdjustmentResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<StockAdjustmentResponse>> AddAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<StockAdjustmentResponse>> UpdateAsync(
        int id,
        StockAdjustmentUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
