using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.CashVouchers;

public interface ICashVoucherService
{
    Task<Result<PagedResponse<CashVoucherResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CashVoucherFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<CashVoucherResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<CashVoucherPartySelectResponse>> GetPartySelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<CashVoucherResponse>> AddAsync(
        CashVoucherRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CashVoucherBulkResponse>> BulkAsync(
        CashVoucherBulkRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CashVoucherResponse>> UpdateAsync(
        int id,
        CashVoucherUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
