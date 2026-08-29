using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.EmployeeOpeningBalances;

public interface IEmployeeOpeningBalanceService
{
    Task<Result<PagedResponse<EmployeeOpeningBalanceResponse>>> GetAllAsync(
        PaginationRequest pagination,
        EmployeeOpeningBalanceFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeOpeningBalanceResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeOpeningBalanceResponse>> AddAsync(
        EmployeeOpeningBalanceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeOpeningBalanceResponse>> UpdateAsync(
        int id,
        EmployeeOpeningBalanceUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
