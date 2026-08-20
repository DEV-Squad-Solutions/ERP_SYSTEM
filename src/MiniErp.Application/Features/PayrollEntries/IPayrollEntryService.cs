using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.PayrollEntries;

public interface IPayrollEntryService
{
    Task<Result<PagedResponse<PayrollEntriesListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        PayrollEntryFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollEntryResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollEntryResponse>> AddAsync(
        PayrollEntryCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollEntryResponse>> PaySalaryAsync(
        int id,
        PayrollEntrySalaryPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
