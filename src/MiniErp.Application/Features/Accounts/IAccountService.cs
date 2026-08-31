using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Accounts;

public interface IAccountService
{
    Task<Result<PagedResponse<AccountResponse>>> GetAllAsync(
        PaginationRequest pagination,
        AccountFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AccountTreeResponse>>> GetTreeAsync(
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AccountSelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<AccountResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<AccountResponse>> AddAsync(
        AccountRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AccountResponse>> UpdateAsync(
        int id,
        AccountUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
