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
                IsActive: true,
                IsDefaultForSales: false,
                IsDefaultForPurchase: false,
                IsDefaultForSalesReturn: false,
                IsDefaultForPurchaseReturn: false,
                Notes: null));

        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(CashMovementTypeRequest.Direction));
    }

    [Theory]
    [InlineData(false, true, CashDirection.Receipt)]
    [InlineData(true, false, CashDirection.Receipt)]
    [InlineData(true, true, CashDirection.Payment)]
    public void MovementTypeValidator_RejectsInvalidInvoiceDefault(
        bool forPartner,
        bool isActive,
        CashDirection direction)
    {
        var validator = new CashMovementTypeRequestValidator();
        var result = validator.Validate(
            new CashMovementTypeRequest(
                "Invoice default",
                direction,
                forPartner,
                isActive,
                IsDefaultForSales: true,
                IsDefaultForPurchase: false,
                IsDefaultForSalesReturn: false,
                IsDefaultForPurchaseReturn: false,
                Notes: null));

        Assert.Contains(
            result.Errors,
            error => error.PropertyName ==
                nameof(CashMovementTypeRequest.IsDefaultForSales));
    }

    [Theory]
    [InlineData(CashPartyType.None, null, null, null, null)]
    [InlineData(CashPartyType.Partner, 1, null, null, null)]
    [InlineData(CashPartyType.Driver, null, 1, null, null)]
    [InlineData(CashPartyType.Driver, null, 1, 1, null)]
    [InlineData(CashPartyType.Other, null, null, null, "Outside party")]
    public void CashVoucherUpdateValidator_AcceptsEveryValidPartyShape(
        CashPartyType partyType,
        int? partnerId,
        int? driverId,
        int? tripId,
        string? externalPartyName)
    {
        var validator = new CashVoucherUpdateRequestValidator();
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
    public void CashVoucherUpdateValidator_RejectsMismatchedPartyFieldsAndAmount()
    {
        var validator = new CashVoucherUpdateRequestValidator();
        var result = validator.Validate(
            new CashVoucherUpdateRequest(
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
                null,
                new byte[8]));

        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(CashVoucherUpdateRequest.BusinessPartnerId));
        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(CashVoucherUpdateRequest.DriverId));
        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(CashVoucherUpdateRequest.DriverTripId));
        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(CashVoucherUpdateRequest.ExternalPartyName));
        Assert.Contains(
            result.Errors,
            error => error.PropertyName ==
                nameof(CashVoucherUpdateRequest.Amount));
    }

    [Fact]
    public void CashVoucherValidator_AcceptsInitialDraftFields()
    {
        var validator = new CashVoucherRequestValidator();
        var result = validator.Validate(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 1),
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                Amount: 125m,
                Description: "Initial receipt"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CashVoucherContracts_DoNotAcceptVoucherNumber()
    {
        Assert.Equal(
            ["Amount", "CashboxId", "Description", "Direction", "VoucherDate"],
            typeof(CashVoucherRequest)
                .GetProperties()
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray());
        Assert.DoesNotContain(
            typeof(CashVoucherUpdateRequest).GetProperties(),
            property => property.Name == "VoucherNumber");
    }

    [Fact]
    public void CashVoucherValidator_RequiresCashbox()
    {
        var validator = new CashVoucherRequestValidator();
        var result = validator.Validate(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 1),
                Direction: CashDirection.Payment,
                CashboxId: 0,
                Amount: 50m,
                Description: "Initial payment"));

        Assert.Contains(
            result.Errors,
            error => error.PropertyName ==
                nameof(CashVoucherRequest.CashboxId));
    }

    [Fact]
    public void UpdateValidators_RequireEightByteRowVersions()
    {
        var cashboxResult = new CashboxUpdateRequestValidator().Validate(
            new CashboxUpdateRequest(
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
                    IsActive: true,
                    IsDefaultForSales: false,
                    IsDefaultForPurchase: false,
                    IsDefaultForSalesReturn: false,
                    IsDefaultForPurchaseReturn: false,
                    Notes: null,
                    RowVersion: null));
        var voucherResult = new CashVoucherUpdateRequestValidator().Validate(
            new CashVoucherUpdateRequest(
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

    private static CashVoucherUpdateRequest CreateVoucher(
        CashPartyType partyType,
        int? partnerId,
        int? driverId,
        int? tripId,
        string? externalPartyName) =>
        new(
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
            null,
            new byte[8]);
}
