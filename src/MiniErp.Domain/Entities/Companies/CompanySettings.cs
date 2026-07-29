using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Companies;

public sealed class CompanySettings
{
    public int CompanyId { get; set; }

    public StockBalanceCheckMode StockBalanceCheckMode { get; set; } =
        StockBalanceCheckMode.DateCheck;

    public Company Company { get; set; } = null!;
}
