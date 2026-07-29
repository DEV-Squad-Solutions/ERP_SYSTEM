using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Inventory;

public sealed class ItemMovement : AuditableEntity
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public int StoreId { get; set; }

    public Store Store { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public int? ItemUnitId { get; set; }

    public ItemUnit? ItemUnit { get; set; }

    public ItemMovementType MovementType { get; set; }

    public int ReferenceId { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public DateOnly MovementDate { get; set; }

    public decimal QuantityIn { get; set; }

    public decimal QuantityOut { get; set; }

    public InventoryCostStatus CostStatus { get; private set; } =
        InventoryCostStatus.Final;

    public decimal PendingCostQuantity { get; private set; }

    public decimal? UnitCost { get; private set; }

    public decimal TotalCost { get; private set; }

    public decimal QuantityAfter { get; private set; }

    public decimal AverageCostAfter { get; private set; }

    public decimal InventoryValueAfter { get; private set; }

    public string? Description { get; set; }

    public ICollection<InventoryCostAllocation> OutboundCostAllocations
    {
        get;
        set;
    } = [];

    public ICollection<InventoryCostAllocation> InboundCostAllocations
    {
        get;
        set;
    } = [];

    public void ApplyCostSnapshot(
        InventoryCostStatus costStatus,
        decimal pendingCostQuantity,
        decimal? unitCost,
        decimal totalCost,
        decimal quantityAfter,
        decimal averageCostAfter,
        decimal inventoryValueAfter)
    {
        CostStatus = costStatus;
        PendingCostQuantity =
            InventoryCostRules.RoundQuantity(pendingCostQuantity);
        UnitCost = unitCost.HasValue
            ? InventoryCostRules.RoundUnitCost(unitCost.Value)
            : null;
        TotalCost = InventoryCostRules.RoundValue(totalCost);
        QuantityAfter = InventoryCostRules.RoundQuantity(quantityAfter);
        AverageCostAfter =
            InventoryCostRules.RoundUnitCost(averageCostAfter);
        InventoryValueAfter =
            InventoryCostRules.RoundValue(inventoryValueAfter);
    }
}
