using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Companies;

public sealed class CompanySettings
{
    public int CompanyId { get; set; }

    public CurrencyCode BaseCurrency { get; set; } = CurrencyCode.EGP;

    public StockBalanceCheckMode StockBalanceCheckMode { get; set; } =
        StockBalanceCheckMode.None;

    public Company Company { get; set; } = null!;
}
