using MiniErp.Application.Features.PartnerOpeningBalances;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.PartnerOpeningBalances;

public sealed class PartnerOpeningBalanceRequestValidatorTests
{
    private readonly PartnerOpeningBalanceRequestValidator validator = new();

    [Fact]
    public void Validate_AcceptsValidReceivableRequest()
    {
        var result = validator.Validate(CreateRequest());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_RejectsNonPositiveAmount(decimal amount)
    {
        var result = validator.Validate(CreateRequest(amount: amount));

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(PartnerOpeningBalanceRequest.Amount));
    }

    [Fact]
    public void Validate_RejectsAmountWithMoreThanTwoDecimalPlaces()
    {
        var result = validator.Validate(CreateRequest(amount: 10.001m));

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(PartnerOpeningBalanceRequest.Amount));
    }

    [Fact]
    public void Validate_RejectsUndefinedCurrencyAndBalanceType()
    {
        var result = validator.Validate(
            CreateRequest(
                currency: (CurrencyCode)999,
                balanceType: (PartnerBalanceType)999));

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(PartnerOpeningBalanceRequest.Currency));
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(PartnerOpeningBalanceRequest.BalanceType));
    }

    [Fact]
    public void Validate_RejectsWhitespaceDocumentNumberAndOversizedNotes()
    {
        var result = validator.Validate(
            CreateRequest(
                documentNumber: "   ",
                notes: new string('x', PartnerOpeningBalanceRequest.NotesMaximumLength + 1)));

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(PartnerOpeningBalanceRequest.DocumentNumber));
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(PartnerOpeningBalanceRequest.Notes));
    }

    [Fact]
    public void UpdateValidator_RequiresRowVersion()
    {
        var validator = new PartnerOpeningBalanceUpdateRequestValidator();
        var request = new PartnerOpeningBalanceUpdateRequest(
            1,
            "OPEN-001",
            new DateOnly(2026, 1, 1),
            CurrencyCode.EGP,
            PartnerBalanceType.Receivable,
            10m,
            null,
            null);

        var result = validator.Validate(request);

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(PartnerOpeningBalanceUpdateRequest.RowVersion));
    }

    private static PartnerOpeningBalanceRequest CreateRequest(
        string documentNumber = "OPEN-001",
        CurrencyCode currency = CurrencyCode.EGP,
        PartnerBalanceType balanceType = PartnerBalanceType.Receivable,
        decimal amount = 125.50m,
        string? notes = null) =>
        new(
            1,
            documentNumber,
            new DateOnly(2026, 1, 1),
            currency,
            balanceType,
            amount,
            notes);
}
