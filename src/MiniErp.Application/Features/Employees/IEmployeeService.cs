using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Employees
{
    public interface IEmployeeService
    {
        Task<Result<EmployeePageResponse>> GetAllAsync(
            PaginationRequest pagination,
            EmployeeFilterRequest? filters = null,
            CancellationToken cancellationToken = default);

        Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
            CancellationToken cancellationToken = default);

        Task<Result<EmployeeResponse>> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<Result<EmployeeResponse>> AddAsync(
            EmployeeRequest request,
            CancellationToken cancellationToken = default);

        Task<Result<EmployeeResponse>> UpdateAsync(
            int id,
            EmployeeUpdateRequest request,
            CancellationToken cancellationToken = default);

        Task<Result> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default);
    }
}
