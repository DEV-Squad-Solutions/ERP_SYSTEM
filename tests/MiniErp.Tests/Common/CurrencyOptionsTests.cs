using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Controllers;
using MiniErp.Application.Features.Currencies;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.Common;

public sealed class CurrencyOptionsTests
{
    [Theory]
    [InlineData(CurrencyCode.EGP, "الجنيه المصري")]
    [InlineData(CurrencyCode.USD, "الدولار الأمريكي")]
    [InlineData(CurrencyCode.EUR, "اليورو")]
    [InlineData(CurrencyCode.GBP, "الجنيه الإسترليني")]
    [InlineData(CurrencyCode.SAR, "الريال السعودي")]
    [InlineData(CurrencyCode.AED, "الدرهم الإماراتي")]
    [InlineData(CurrencyCode.KWD, "الدينار الكويتي")]
    public void GetDescription_ReturnsArabicName(
        CurrencyCode currency,
        string expectedDescription)
    {
        Assert.Equal(expectedDescription, currency.GetDescription());
    }

    [Fact]
    public void GetSelect_ReturnsEveryCurrencyWithItsArabicDescription()
    {
        var controller = new CurrenciesController();

        var actionResult = controller.GetSelect();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var options = Assert.IsAssignableFrom<
            IReadOnlyList<CurrencyOptionResponse>>(okResult.Value);

        Assert.Equal(Enum.GetValues<CurrencyCode>(), options.Select(x => x.Value));
        Assert.All(
            options,
            option => Assert.Equal(
                option.Value.GetDescription(),
                option.Description));
    }
}
