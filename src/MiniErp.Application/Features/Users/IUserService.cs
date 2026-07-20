using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Users;

public interface IUserService : IScopedService
{
    Task<Result<PagedResponse<UserResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    Task<Result<UserResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<string>>> GetRolesAsync(
        CancellationToken cancellationToken = default);

    Task<Result<UserResponse>> AddAsync(
        UserCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<UserResponse>> UpdateAsync(
        Guid id,
        UserUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<UserResponse>> AssignCompaniesAsync(
        Guid id,
        UserCompaniesRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
