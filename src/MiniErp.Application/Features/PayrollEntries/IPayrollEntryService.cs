using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.PayrollEntries;

public interface IPayrollEntryService
{
    Task<Result<PagedResponse<PayrollEntryResponse>>> GetAllAsync(
        PaginationRequest pagination,
        PayrollEntryFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollEntryResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollEntryResponse>> AddAsync(
        PayrollEntryRequest request,
        CancellationToken cancellationToken = default);

    //Task<Result<PayrollEntryResponse>> UpdateAsync(
    //    int id,
    //    PayrollEntryRequest request,
    //    CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
