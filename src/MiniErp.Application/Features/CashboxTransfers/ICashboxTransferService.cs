using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.CashboxTransfers;

public interface ICashboxTransferService
{
    Task<Result<PagedResponse<CashboxTransferListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CashboxTransferFilterRequest filters,
        CancellationToken cancellationToken = default);

    Task<Result<CashboxTransferResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<CashboxTransferResponse>> AddAsync(
        CashboxTransferRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CashboxTransferResponse>> UpdateAsync(
        int id,
        CashboxTransferUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
