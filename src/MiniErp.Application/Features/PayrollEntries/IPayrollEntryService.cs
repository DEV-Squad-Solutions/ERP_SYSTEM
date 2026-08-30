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

    Task<Result<List<PayrollEntryResponse>>> AddBulkAsync(
        BulkPayrollEntryCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollEntryResponse>> MoveSalaryForEmployeeAccountAsync(
        int id,
        PayrollEntrySalaryPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<List<PayrollEntryResponse>>> MoveSalaryForEmployeeAccountBulkAsync(
        BulkPayrollEntrySalaryPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollEntryResponse>> UpdateAsync(
        int id,
        PayrollEntryUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<List<PayrollEntryResponse>>> UpdateBulkAsync(
        BulkPayrollEntryUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollEntryResponse>> RecalculateAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteBulkAsync(
        BulkPayrollEntryDeleteRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollDashboardResponse>> GetDashboardAsync(
        PayrollDashboardFilterRequest? filters = null,
        CancellationToken cancellationToken = default);
}
