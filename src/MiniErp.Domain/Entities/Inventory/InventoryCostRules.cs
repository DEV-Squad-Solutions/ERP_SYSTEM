namespace MiniErp.Domain.Entities.Inventory;

public static class InventoryCostRules
{
    public const int QuantityPrecision = 18;

    public const int QuantityScale = 6;

    public const int UnitCostPrecision = 24;

    public const int UnitCostScale = 8;

    public const int ValuePrecision = 28;

    public const int ValueScale = 8;

    public static decimal RoundQuantity(decimal value) =>
        decimal.Round(
            value,
            QuantityScale,
            MidpointRounding.AwayFromZero);

    public static decimal RoundUnitCost(decimal value) =>
        decimal.Round(
            value,
            UnitCostScale,
            MidpointRounding.AwayFromZero);

    public static decimal RoundValue(decimal value) =>
        decimal.Round(
            value,
            ValueScale,
            MidpointRounding.AwayFromZero);

    public static decimal CalculateTotal(
        decimal quantity,
        decimal unitCost) =>
        RoundValue(quantity * unitCost);

    public static decimal CalculateAverage(
        decimal inventoryValue,
        decimal positiveQuantity)
    {
        if (positiveQuantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(positiveQuantity),
                "Average cost can only be calculated for positive quantity.");
        }

        return RoundUnitCost(inventoryValue / positiveQuantity);
    }
}
