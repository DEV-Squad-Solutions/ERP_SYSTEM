using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.InventoryCounts;

public interface IInventoryCountService
{
    Task<Result<PagedResponse<InventoryCountListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        InventoryCountFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<InventoryCountResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<InventoryCountResponse>> AddAsync(
        InventoryCountRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<InventoryCountResponse>> UpdateAsync(
        int id,
        InventoryCountUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<InventoryCountResponse>> ReconcileAsync(
        int id,
        InventoryCountReconcileRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
