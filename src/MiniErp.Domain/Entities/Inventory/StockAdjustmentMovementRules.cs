using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Inventory;

public static class StockAdjustmentMovementRules
{
    public static ItemMovementType GetMovementType(
        StockAdjustmentDirection direction) =>
        direction switch
        {
            StockAdjustmentDirection.Increase =>
                ItemMovementType.AdjustmentIncrease,
            StockAdjustmentDirection.Decrease =>
                ItemMovementType.AdjustmentDecrease,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };

    public static bool IsInbound(StockAdjustmentDirection direction) =>
        direction == StockAdjustmentDirection.Increase;
}
