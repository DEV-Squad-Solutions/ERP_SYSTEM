namespace MiniErp.Application.Features.Users;

public sealed record UserCashboxPermissionsResponse(
    Guid UserId,
    int CompanyId,
    bool AllCashboxes,
    Guid Revision,
    IReadOnlyList<UserCashboxPermissionGrant> Permissions);
