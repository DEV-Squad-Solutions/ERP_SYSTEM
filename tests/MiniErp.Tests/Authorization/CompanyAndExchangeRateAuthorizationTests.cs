using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using MiniErp.Api.Controllers;

namespace MiniErp.Tests.Authorization;

public sealed class CompanyAndExchangeRateAuthorizationTests
{
    [Fact]
    public void CompaniesController_RemainsAdminOnly()
    {
        var authorize = typeof(CompaniesController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .Single(attribute => attribute.Roles == "Admin");

        Assert.Equal("Admin", authorize.Roles);
    }

    [Theory]
    [InlineData(nameof(ExchangeRatesController.Create))]
    [InlineData(nameof(ExchangeRatesController.Update))]
    [InlineData(nameof(ExchangeRatesController.Delete))]
    [InlineData(nameof(ExchangeRatesController.Import))]
    [InlineData(nameof(ExchangeRatesController.PreviewImport))]
    public void ExchangeRateMutations_RemainAdminOnly(string methodName)
    {
        var method = typeof(ExchangeRatesController).GetMethod(methodName);
        var authorize = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Admin", authorize!.Roles);
    }

    [Fact]
    public void ExchangeRateList_DoesNotAddAnAuthorizationOverride()
    {
        var method = typeof(ExchangeRatesController)
            .GetMethod(nameof(ExchangeRatesController.GetAll));

        Assert.NotNull(method);
        Assert.Null(method!.GetCustomAttribute<AuthorizeAttribute>());
    }
}
