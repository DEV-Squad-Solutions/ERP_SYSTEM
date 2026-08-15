using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.Inventory;

public sealed class ItemStoreBalance : AuditableEntity
{
    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public decimal Quantity { get; private set; }

    public decimal AverageCost { get; private set; }

    public decimal InventoryValue { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void Apply(
        decimal quantity,
        decimal averageCost,
        decimal inventoryValue)
    {
        Quantity = InventoryCostRules.RoundQuantity(quantity);

        if (Quantity <= 0m)
        {
            AverageCost = 0m;
            InventoryValue = 0m;
            return;
        }

        AverageCost = InventoryCostRules.RoundUnitCost(averageCost);
        InventoryValue = InventoryCostRules.RoundValue(inventoryValue);
    }
}
