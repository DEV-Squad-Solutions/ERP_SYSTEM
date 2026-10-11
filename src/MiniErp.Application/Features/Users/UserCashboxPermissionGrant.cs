namespace MiniErp.Application.Features.Users;

public sealed record UserCashboxPermissionGrant(int CashboxId, bool CanView, bool CanExecute);
