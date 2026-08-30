using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.EmployeeMovements;

public interface IEmployeeMovementService
{
    Task<Result<PagedResponse<EmployeeMovementResponse>>> GetAllAsync(
        PaginationRequest pagination,
        EmployeeMovementFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeMovementResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeMovementResponse>> AddAsync(
        EmployeeMovementRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<List<EmployeeMovementResponse>>> AddBulkAsync(
        BulkEmployeeMovementRequest request,
        CancellationToken cancellationToken = default);
}
