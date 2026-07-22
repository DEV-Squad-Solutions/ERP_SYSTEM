using MiniErp.Domain.Common.Entities;

namespace MiniErp.Domain.Entities;

public sealed class StockOpeningBalanceLine : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int StockOpeningBalanceId { get; set; }

    public StockOpeningBalance StockOpeningBalance { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public int ItemUnitId { get; set; }

    public ItemUnit ItemUnit { get; set; } = null!;

    public decimal Quantity { get; set; }

    public string? Notes { get; set; }

}
