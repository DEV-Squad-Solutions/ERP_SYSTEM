namespace MiniErp.Domain.Entities.Companies;

public static class ExchangeRateRules
{
    public const int RatePrecision = 28;

    public const int RateScale = 12;

    public const int BaseAmountPrecision = 28;

    public const int BaseAmountScale = 8;

    public static bool IsValidRate(decimal value) =>
        value > 0m &&
        decimal.Round(value, RateScale) == value;

    public static decimal RoundRate(decimal value) =>
        decimal.Round(
            value,
            RateScale,
            MidpointRounding.AwayFromZero);

    public static decimal ConvertToBase(
        decimal amount,
        decimal exchangeRate) =>
        RoundBaseAmount(amount * exchangeRate);

    public static decimal ConvertFromBase(
        decimal baseAmount,
        decimal exchangeRate)
    {
        if (!IsValidRate(exchangeRate))
        {
            throw new ArgumentOutOfRangeException(
                nameof(exchangeRate),
                "Exchange rate must be greater than zero.");
        }

        return decimal.Round(
            baseAmount / exchangeRate,
            2,
            MidpointRounding.AwayFromZero);
    }

    public static decimal RoundBaseAmount(decimal value) =>
        decimal.Round(
            value,
            BaseAmountScale,
            MidpointRounding.AwayFromZero);
}
