using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.EmployeeAttendance;

public interface IEmployeeAttendanceService
{
    Task<Result<PagedResponse<EmployeeAttendanceResponse>>> GetAllAsync(
        PaginationRequest pagination,
        EmployeeAttendanceFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAttendanceResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAttendanceResponse>> AddAsync(
        EmployeeAttendanceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<List<EmployeeAttendanceResponse>>> AddBulkAsync(
        BulkEmployeeAttendanceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAttendanceResponse>> UpdateAsync(
        int id,
        EmployeeAttendanceUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<List<EmployeeAttendanceResponse>>> UpdateBulkAsync(
        BulkEmployeeAttendanceUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteBulkAsync(
        BulkEmployeeAttendanceDeleteRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAttendanceReportResponse>> GetReportAsync(
        EmployeeAttendanceReportRequest request,
        CancellationToken cancellationToken = default);
}
