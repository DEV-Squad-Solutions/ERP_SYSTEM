using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Domain.Entities.Inventory;

public sealed class InventoryCostAllocation
{
    public long Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public int OutboundMovementId { get; set; }

    public ItemMovement OutboundMovement { get; set; } = null!;

    public int InboundMovementId { get; set; }

    public ItemMovement InboundMovement { get; set; } = null!;

    public decimal Quantity { get; private set; }

    public decimal UnitCost { get; private set; }

    public decimal TotalCost { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public static InventoryCostAllocation Create(
        int companyId,
        int storeId,
        int itemId,
        int outboundMovementId,
        int inboundMovementId,
        decimal quantity,
        decimal unitCost,
        DateTime createdOn)
    {
        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (unitCost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCost));
        }

        return new InventoryCostAllocation
        {
            CompanyId = companyId,
            StoreId = storeId,
            ItemId = itemId,
            OutboundMovementId = outboundMovementId,
            InboundMovementId = inboundMovementId,
            Quantity = InventoryCostRules.RoundQuantity(quantity),
            UnitCost = InventoryCostRules.RoundUnitCost(unitCost),
            TotalCost = InventoryCostRules.CalculateTotal(
                quantity,
                unitCost),
            CreatedOn = createdOn
        };
    }
}
