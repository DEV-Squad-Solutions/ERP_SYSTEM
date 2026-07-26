using MiniErp.Application.Common.Parsing;

namespace MiniErp.Tests.Common;

public sealed class FlexibleDateOnlyParserTests
{
    [Theory]
    [InlineData("2026-07-31")]
    [InlineData("2026/07/31")]
    [InlineData("31/07/2026")]
    [InlineData("31-07-2026")]
    [InlineData("31.07.2026")]
    [InlineData("7/31/2026")]
    [InlineData("July 31, 2026")]
    [InlineData("31 July 2026")]
    [InlineData("٣١/٠٧/٢٠٢٦")]
    [InlineData("۳۱/۰۷/۲۰۲۶")]
    public void TryParse_AcceptsSupportedDateFormats(string value)
    {
        var parsed = FlexibleDateOnlyParser.TryParse(
            value,
            out var date);

        Assert.True(parsed);
        Assert.Equal(new DateOnly(2026, 7, 31), date);
    }

    [Fact]
    public void TryParse_UsesDayFirstForAmbiguousNumericDates()
    {
        var parsed = FlexibleDateOnlyParser.TryParse(
            "01/02/2026",
            out var date);

        Assert.True(parsed);
        Assert.Equal(new DateOnly(2026, 2, 1), date);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    [InlineData("31/02/2026")]
    public void TryParse_RejectsInvalidDates(string? value)
    {
        Assert.False(
            FlexibleDateOnlyParser.TryParse(value, out _));
    }
}
