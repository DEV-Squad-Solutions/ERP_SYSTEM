using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.Inventory;

public sealed class InventoryCountLine : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int InventoryCountId { get; set; }

    public InventoryCount InventoryCount { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public int ItemUnitId { get; set; }

    public ItemUnit ItemUnit { get; set; } = null!;

    public decimal SystemQuantity { get; set; }

    public decimal? PhysicalQuantity { get; set; }

    public string? Notes { get; set; }
}
