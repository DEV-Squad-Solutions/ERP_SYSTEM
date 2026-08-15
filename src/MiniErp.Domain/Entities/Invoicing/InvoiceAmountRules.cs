namespace MiniErp.Domain.Entities.Invoicing;

public static class InvoiceAmountRules
{
    public const int QuantityPrecision = 18;

    public const int QuantityScale = 6;

    public const int MoneyPrecision = 18;

    public const int MoneyScale = 2;

    private const decimal QuantityMaximumExclusive = 1_000_000_000_000m;

    private const decimal MoneyMaximumExclusive = 10_000_000_000_000_000m;

    public static bool TryCalculate(
        int count,
        decimal weight,
        decimal price,
        out decimal quantity,
        out decimal total)
    {
        quantity = 0;
        total = 0;

        if (count <= 0 ||
            weight <= 0 ||
            price < 0 ||
            !HasPrecision(weight, QuantityMaximumExclusive, QuantityScale) ||
            !HasPrecision(price, MoneyMaximumExclusive, MoneyScale))
        {
            return false;
        }

        try
        {
            quantity = count * weight;
            total = decimal.Round(
                quantity * price,
                MoneyScale,
                MidpointRounding.AwayFromZero);
        }
        catch (OverflowException)
        {
            quantity = 0;
            total = 0;
            return false;
        }

        return HasPrecision(quantity, QuantityMaximumExclusive, QuantityScale) &&
            HasPrecision(total, MoneyMaximumExclusive, MoneyScale);
    }

    public static bool TryCalculate(
        int? count,
        decimal? weight,
        decimal price,
        out decimal quantity,
        out decimal total)
    {
        if (!count.HasValue || !weight.HasValue)
        {
            quantity = 0m;
            total = 0m;
            return false;
        }

        return TryCalculate(
            count.Value,
            weight.Value,
            price,
            out quantity,
            out total);
    }

    public static bool IsValidMoney(decimal value) =>
        HasPrecision(value, MoneyMaximumExclusive, MoneyScale);

    public static bool IsValidQuantity(decimal value) =>
        HasPrecision(value, QuantityMaximumExclusive, QuantityScale);

    private static bool HasPrecision(
        decimal value,
        decimal maximumExclusive,
        int scale) =>
        value >= 0 &&
        value < maximumExclusive &&
        decimal.Round(value, scale) == value;
}
