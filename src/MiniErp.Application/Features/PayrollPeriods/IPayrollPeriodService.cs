using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.PayrollPeriods;

public interface IPayrollPeriodService
{
    Task<Result<PagedResponse<PayrollPeriodListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        PayrollPeriodFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollPeriodResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollPeriodResponse>> CreateAsync(
        PayrollPeriodCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollPeriodResponse>> UpdateAsync(
        int id,
        PayrollPeriodUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates all payroll entries that overlap with this period's date range,
    /// computes summary totals, and transitions status to Calculated.
    /// </summary>
    Task<Result<PayrollPeriodResponse>> CalculatePeriodAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a full employee-level report for a stored payroll period.
    /// </summary>
    Task<Result<PayrollPeriodReportResponse>> GetReportByPeriodAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a full employee-level report for any arbitrary date range
    /// (not tied to a stored PayrollPeriod).
    /// </summary>
    Task<Result<PayrollPeriodReportResponse>> GetReportByDateRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}
