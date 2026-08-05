using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.EmployeeTransactions;

public interface IEmployeeTransactionService
{
    Task<Result<PagedResponse<EmployeeTransactionResponse>>> GetAllAsync(
        PaginationRequest pagination,
        EmployeeTransactionFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeTransactionResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeTransactionResponse>> AddAsync(
        EmployeeTransactionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeTransactionResponse>> UpdateAsync(
        int id,
        EmployeeTransactionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
