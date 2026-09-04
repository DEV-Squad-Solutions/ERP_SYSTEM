using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.PayrollReport;

public interface IPayrollReportService
{
    Task<Result<PayrollReportResponse>> BuildReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        int? employeeId = null,
        bool? isMoved = null,
        CancellationToken cancellationToken = default);
}
