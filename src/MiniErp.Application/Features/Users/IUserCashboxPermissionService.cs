using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Users;

public interface IUserCashboxPermissionService
{
    Task<Result<UserCashboxPermissionsResponse>> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<UserCashboxPermissionsResponse>> ReplaceAsync(Guid userId, UserCashboxPermissionsRequest request, CancellationToken cancellationToken = default);
}
