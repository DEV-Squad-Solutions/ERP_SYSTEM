using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Infrastructure.Identity;

public sealed class UserCashboxPermission
{
    public Guid UserId { get; set; }
    public int CompanyId { get; set; }
    public int CashboxId { get; set; }
    public bool CanView { get; set; }
    public bool CanExecute { get; set; }
    public UserCompany UserCompany { get; set; } = null!;
    public Cashbox Cashbox { get; set; } = null!;
}
