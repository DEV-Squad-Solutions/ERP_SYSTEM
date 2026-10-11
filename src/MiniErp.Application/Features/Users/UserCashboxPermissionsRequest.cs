namespace MiniErp.Application.Features.Users;

public sealed record UserCashboxPermissionsRequest(
    bool AllCashboxes,
    Guid Revision,
    IReadOnlyCollection<UserCashboxPermissionGrant> Permissions);
