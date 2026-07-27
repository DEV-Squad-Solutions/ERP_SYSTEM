using MiniErp.Application.Features.CashMovementTypes;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.Cashboxes;
using MiniErp.Application.Features.DriverTrips;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.CashManagement;

public sealed class CashManagementValidatorTests
{
    [Fact]
    public void CashboxValidator_RejectsInvalidMoneyAndUndefinedCurrency()
    {
        var validator = new CashboxRequestValidator();
        var result = validator.Validate(
            new CashboxRequest(
                "MAIN",
                "Main",
                (CurrencyCode)999,
                -1.001m,
                true,
                null));

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CashboxRequest.Currency));
        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName == nameof(CashboxRequest.OpeningBalance));
    }

    [Fact]
    public void MovementTypeValidator_RejectsUndefinedDirection()
    {
        var validator = new CashMovementTypeRequestValidator();
        var result = validator.Validate(
            new CashMovementTypeRequest(
                "Type",
                (CashDirection)999,
                ForPartner: true,
                true,
                null));

        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(CashMovementTypeRequest.Direction));
    }

    [Theory]
    [InlineData(CashPartyType.None, null, null, null, null)]
    [InlineData(CashPartyType.Partner, 1, null, null, null)]
    [InlineData(CashPartyType.Driver, null, 1, null, null)]
    [InlineData(CashPartyType.Driver, null, 1, 1, null)]
    [InlineData(CashPartyType.Other, null, null, null, "Outside party")]
    public void CashVoucherValidator_AcceptsEveryValidPartyShape(
        CashPartyType partyType,
        int? partnerId,
        int? driverId,
        int? tripId,
        string? externalPartyName)
    {
        var validator = new CashVoucherRequestValidator();
        var result = validator.Validate(
            CreateVoucher(
                partyType,
                partnerId,
                driverId,
                tripId,
                externalPartyName));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CashVoucherValidator_RejectsMismatchedPartyFieldsAndAmount()
    {
        var validator = new CashVoucherRequestValidator();
        var result = validator.Validate(
            new CashVoucherRequest(
                "CV-1",
                new DateOnly(2026, 7, 27),
                CashDirection.Payment,
                1,
                4,
                CashPartyType.Partner,
                null,
                1,
                1,
                "Invalid",
                0m,
                null,
                null,
                null));

        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(CashVoucherRequest.BusinessPartnerId));
        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName == nameof(CashVoucherRequest.DriverId));
        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(CashVoucherRequest.DriverTripId));
        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(CashVoucherRequest.ExternalPartyName));
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CashVoucherRequest.Amount));
    }

    [Fact]
    public void UpdateValidators_RequireEightByteRowVersions()
    {
        var cashboxResult = new CashboxUpdateRequestValidator().Validate(
            new CashboxUpdateRequest(
                "MAIN",
                "Main",
                CurrencyCode.EGP,
                0m,
                true,
                null,
                [1]));
        var movementResult =
            new CashMovementTypeUpdateRequestValidator().Validate(
                new CashMovementTypeUpdateRequest(
                    "Type",
                    CashDirection.Receipt,
                    ForPartner: false,
                    true,
                    null,
                    null));
        var voucherResult = new CashVoucherUpdateRequestValidator().Validate(
            new CashVoucherUpdateRequest(
                "CV-1",
                new DateOnly(2026, 7, 27),
                CashDirection.Receipt,
                1,
                3,
                CashPartyType.None,
                null,
                null,
                null,
                null,
                1m,
                null,
                null,
                null,
                []));

        Assert.Contains(
            cashboxResult.Errors,
            error =>
                error.PropertyName == nameof(CashboxUpdateRequest.RowVersion));
        Assert.Contains(
            movementResult.Errors,
            error =>
                error.PropertyName ==
                nameof(CashMovementTypeUpdateRequest.RowVersion));
        Assert.Contains(
            voucherResult.Errors,
            error =>
                error.PropertyName ==
                nameof(CashVoucherUpdateRequest.RowVersion));
    }

    [Fact]
    public void DriverTripBulkValidator_RejectsDuplicatesAndNegativeCost()
    {
        var validator = new DriverTripBulkCostUpdateRequestValidator();
        var result = validator.Validate(
            new DriverTripBulkCostUpdateRequest(
            [
                new DriverTripCostUpdateItem(1, 10m, null, new byte[8]),
                new DriverTripCostUpdateItem(1, -1m, null, new byte[8])
            ]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName.Contains(
                nameof(DriverTripCostUpdateItem.Cost),
                StringComparison.Ordinal));
        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(DriverTripBulkCostUpdateRequest.Items));
    }

    private static CashVoucherRequest CreateVoucher(
        CashPartyType partyType,
        int? partnerId,
        int? driverId,
        int? tripId,
        string? externalPartyName) =>
        new(
            "CV-1",
            new DateOnly(2026, 7, 27),
            CashDirection.Receipt,
            1,
            3,
            partyType,
            partnerId,
            driverId,
            tripId,
            externalPartyName,
            10m,
            null,
            null,
            null);
}
