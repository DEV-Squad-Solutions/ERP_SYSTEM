using MiniErp.Application.Features.StockOpeningBalances;

namespace MiniErp.Tests.StockOpeningBalances;

public sealed class StockOpeningBalanceRequestValidatorTests
{
    private readonly StockOpeningBalanceRequestValidator validator = new();

    [Fact]
    public void Validate_AcceptsMaximumDecimal18Scale6Weight()
    {
        var request = CreateRequest(
            weight: 999_999_999_999.999999m,
            price: 0m);

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("1000000000000")]
    [InlineData("0.0000001")]
    public void Validate_RejectsWeightOutsideDecimal18Scale6(string value)
    {
        var request = CreateRequest(weight: decimal.Parse(value));

        var result = validator.Validate(request);

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName.EndsWith(
                nameof(StockOpeningBalanceLineRequest.Weight),
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("10000000000000000")]
    [InlineData("0.001")]
    public void Validate_RejectsPriceOutsideDecimal18Scale2(string value)
    {
        var request = CreateRequest(price: decimal.Parse(value));

        var result = validator.Validate(request);

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName.EndsWith(
                nameof(StockOpeningBalanceLineRequest.Price),
                StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsCalculatedQuantityOverflow()
    {
        var request = CreateRequest(
            count: 2,
            weight: 999_999_999_999.999999m,
            price: 0m);

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsOversizedHeaderAndLineNotes()
    {
        var oversizedNotes = new string(
            'x',
            StockOpeningBalanceRequest.NotesMaximumLength + 1);
        var request = new StockOpeningBalanceRequest(
            1,
            new DateOnly(2026, 1, 1),
            [new StockOpeningBalanceLineRequest(1, 1, 1m, 1m, oversizedNotes)],
            oversizedNotes);

        var result = validator.Validate(request);

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName ==
                nameof(StockOpeningBalanceRequest.Notes));
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName.EndsWith(
                nameof(StockOpeningBalanceLineRequest.Notes),
                StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsNullLineWithoutThrowing()
    {
        var request = new StockOpeningBalanceRequest(
            1,
            new DateOnly(2026, 1, 1),
            [null!],
            null);

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName ==
                nameof(StockOpeningBalanceRequest.Lines));
    }

    private static StockOpeningBalanceRequest CreateRequest(
        int count = 1,
        decimal weight = 1m,
        decimal price = 1m) =>
        new(
            1,
            new DateOnly(2026, 1, 1),
            [
                new StockOpeningBalanceLineRequest(
                    1,
                    count,
                    weight,
                    price,
                    null)
            ],
            null);
}
