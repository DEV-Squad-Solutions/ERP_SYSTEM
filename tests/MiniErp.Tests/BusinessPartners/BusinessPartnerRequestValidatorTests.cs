using MiniErp.Application.Features.BusinessPartners;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.BusinessPartners;

public sealed class BusinessPartnerRequestValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Email_is_optional_when_missing_or_blank(string? email)
    {
        var request = new BusinessPartnerRequest(
            Name: "Customer",
            PhoneNumber: null,
            Email: email,
            Address: null,
            TaxNumber: null,
            Currency: CurrencyCode.EGP,
            CreditLimit: 0m);

        var result = new BusinessPartnerRequestValidator().Validate(request);

        Assert.DoesNotContain(
            result.Errors,
            error => error.PropertyName == nameof(BusinessPartnerRequest.Email));
    }

    [Fact]
    public void Email_must_be_valid_when_supplied()
    {
        var request = new BusinessPartnerRequest(
            Name: "Customer",
            PhoneNumber: null,
            Email: "not-an-email",
            Address: null,
            TaxNumber: null,
            Currency: CurrencyCode.EGP,
            CreditLimit: 0m);

        var result = new BusinessPartnerRequestValidator().Validate(request);

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(BusinessPartnerRequest.Email));
    }
}
