using System.Reflection;
using Microsoft.AspNetCore.Http;
using MiniErp.Api.Errors;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.BusinessPartners;
using MiniErp.Application.Features.Companies;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.Common;

public sealed class ErrorCatalogTests
{
    [Fact]
    public void ParameterlessFeatureErrorFactories_HaveUniqueStableCodes()
    {
        var factories = typeof(CompanyErrors).Assembly
            .GetTypes()
            .Where(type =>
                type.IsAbstract &&
                type.IsSealed &&
                type.Name.EndsWith("Errors", StringComparison.Ordinal) &&
                type.Namespace?.StartsWith(
                    "MiniErp.Application.Features",
                    StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly))
            .Where(method =>
                method.ReturnType == typeof(Error) &&
                method.GetParameters().Length == 0)
            .ToArray();

        var errors = factories
            .Select(factory => Assert.IsType<Error>(factory.Invoke(null, null)))
            .ToArray();

        Assert.NotEmpty(errors);
        Assert.All(errors, error =>
        {
            Assert.False(string.IsNullOrWhiteSpace(error.Code));
            Assert.False(string.IsNullOrWhiteSpace(error.Description));
            Assert.NotEqual(ErrorType.None, error.Type);
        });

        var duplicateCodes = errors
            .GroupBy(error => error.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.Empty(duplicateCodes);
    }

    [Fact]
    public void ParameterizedFeatureErrors_PreserveContextAndFieldNames()
    {
        var companyNotFound = CompanyErrors.NotFound(42);
        var missingRate = ExchangeRateErrors.Missing(
            CurrencyCode.USD,
            new DateOnly(2026, 8, 1));
        var duplicatePartner = BusinessPartnerErrors.NameExists("Acme");

        Assert.Equal("Companies.NotFound", companyNotFound.Code);
        Assert.Contains("42", companyNotFound.Description, StringComparison.Ordinal);
        Assert.Equal(ErrorType.NotFound, companyNotFound.Type);

        Assert.Equal("ExchangeRates.Missing", missingRate.Code);
        Assert.Contains("USD", missingRate.Description, StringComparison.Ordinal);
        Assert.Contains("2026-08-01", missingRate.Description, StringComparison.Ordinal);
        Assert.Equal("exchangeRate", missingRate.FieldName);

        Assert.Equal("BusinessPartners.NameExists", duplicatePartner.Code);
        Assert.Contains("Acme", duplicatePartner.Description, StringComparison.Ordinal);
        Assert.Equal("Name", duplicatePartner.FieldName);
    }

    [Fact]
    public void ApiErrorMapping_PreservesCatalogCodeTypeAndConflictStatus()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/Companies/1";

        var response = ApiErrorResponseFactory.FromError(
            context,
            CompanyErrors.Concurrency());

        Assert.Equal(StatusCodes.Status409Conflict, response.Status);
        Assert.Equal("Companies.Concurrency", response.ErrorCode);
        Assert.Equal(ErrorType.Conflict.ToString(), response.ErrorType);
        Assert.Equal("/api/v1/Companies/1", response.Instance);
    }
}
