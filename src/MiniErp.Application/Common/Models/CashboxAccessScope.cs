namespace MiniErp.Application.Common.Models;

public sealed record CashboxAccessScope(
    bool HasAllCashboxes,
    int[] ViewCashboxIds,
    int[] ExecuteCashboxIds)
{
    public bool CanView(int cashboxId) => HasAllCashboxes || ViewCashboxIds.Contains(cashboxId);
    public bool CanExecute(int cashboxId) => HasAllCashboxes || ExecuteCashboxIds.Contains(cashboxId);
}
