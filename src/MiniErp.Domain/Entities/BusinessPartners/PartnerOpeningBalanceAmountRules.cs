namespace MiniErp.Domain.Entities.BusinessPartners;

public static class PartnerOpeningBalanceAmountRules
{
    public const int MoneyPrecision = 18;

    public const int MoneyScale = 2;

    private const decimal MoneyMaximumExclusive = 10_000_000_000_000_000m;

    public static bool IsValidAmount(decimal amount) =>
        amount > 0 &&
        amount < MoneyMaximumExclusive &&
        decimal.Round(amount, MoneyScale) == amount;
}
