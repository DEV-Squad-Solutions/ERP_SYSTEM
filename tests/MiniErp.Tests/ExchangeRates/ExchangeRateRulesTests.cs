using MiniErp.Domain.Entities.Companies;

namespace MiniErp.Tests.ExchangeRates;

public sealed class ExchangeRateRulesTests
{
    [Fact]
    public void ConvertToBaseUsesDocumentedMultiplicationConvention()
    {
        var result = ExchangeRateRules.ConvertToBase(
            amount: 125.50m,
            exchangeRate: 50.125m);

        Assert.Equal(6_290.68750000m, result);
    }

    [Fact]
    public void ConvertFromBaseRoundsCashboxAmountToTwoDecimals()
    {
        var result = ExchangeRateRules.ConvertFromBase(
            baseAmount: 6_290.6875m,
            exchangeRate: 48.75m);

        Assert.Equal(129.04m, result);
    }

    [Fact]
    public void ConvertFromBaseRejectsZeroRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExchangeRateRules.ConvertFromBase(100m, 0m));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("50.123456789012")]
    public void IsValidRateAcceptsPositiveValuesWithinScale(
        string rawRate)
    {
        Assert.True(ExchangeRateRules.IsValidRate(
            decimal.Parse(
                rawRate,
                System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void IsValidRateRejectsMoreThanTwelveDecimalPlaces()
    {
        Assert.False(ExchangeRateRules.IsValidRate(
            1.1234567890123m));
    }
}
