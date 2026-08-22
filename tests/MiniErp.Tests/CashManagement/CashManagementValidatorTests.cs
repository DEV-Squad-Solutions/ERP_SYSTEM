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
                CashMovementClassification.PartnerSettlement,
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

    [Fact]
    public void MovementTypeValidator_RequiresValidClassificationAndPartnerSettlementParty()
    {
        var validator = new CashMovementTypeRequestValidator();
        var undefined = validator.Validate(
            CreateMovementType((CashMovementClassification)999, true));
        var settlementWithoutPartner = validator.Validate(
            CreateMovementType(
                CashMovementClassification.PartnerSettlement,
                false));

        Assert.Contains(undefined.Errors, error => error.PropertyName ==
            nameof(CashMovementTypeRequest.Classification));
        Assert.Contains(settlementWithoutPartner.Errors, error =>
            error.PropertyName ==
            nameof(CashMovementTypeRequest.Classification));
    }

    [Theory]
    [InlineData(CashMovementClassification.Expense)]
    [InlineData(CashMovementClassification.Revenue)]
    public void MovementTypeValidator_AllowsPartnerForDirectionNeutralClassification(
        CashMovementClassification classification)
    {
        var result = new CashMovementTypeRequestValidator().Validate(
            CreateMovementType(classification, forPartner: true));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void MovementTypeValidator_InvoiceDefaultRequiresPartnerSettlement()
    {
        var request = CreateMovementType(
            CashMovementClassification.Revenue,
            forPartner: true) with
        {
            IsDefaultForSales = true
        };
        var result = new CashMovementTypeRequestValidator().Validate(request);

        Assert.Contains(result.Errors, error => error.PropertyName ==
            nameof(CashMovementTypeRequest.IsDefaultForSales));
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
                CashMovementClassification.PartnerSettlement,
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
    [InlineData(null, null, null, null, null)]
    [InlineData(1, null, null, null, null)]
    [InlineData(null, 1, null, null, null)]
    [InlineData(null, null, 1, null, null)]
    [InlineData(null, null, 1, 1, null)]
    [InlineData(null, null, null, null, "Outside party")]
    public void CashVoucherUpdateValidator_AcceptsEveryValidPartyShape(
        int? employeeId,
        int? partnerId,
        int? driverId,
        int? tripId,
        string? externalPartyName)
    {
        var validator = new CashVoucherUpdateRequestValidator();
        var result = validator.Validate(
            CreateVoucher(
                employeeId,
                partnerId,
                driverId,
                tripId,
                externalPartyName));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CashVoucherSaveValidators_AcceptNullMovementType()
    {
        var updateResult = new CashVoucherUpdateRequestValidator().Validate(
            CreateVoucher(
                employeeId: null,
                partnerId: null,
                driverId: null,
                tripId: null,
                externalPartyName: null) with
            {
                CashMovementTypeId = null
            });
        var bulkResult = new CashVoucherBulkVoucherRequestValidator().Validate(
            CreateBulkVoucher() with
            {
                CashMovementTypeId = null
            });

        Assert.True(updateResult.IsValid);
        Assert.True(bulkResult.IsValid);
    }

    [Fact]
    public void CashVoucherUpdateValidator_RejectsMultiplePartyFieldsAndAmount()
    {
        var validator = new CashVoucherUpdateRequestValidator();
        var result = validator.Validate(
            new CashVoucherUpdateRequest(
                new DateOnly(2026, 7, 27),
                CashDirection.Payment,
                1,
                4,
                1,
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
                nameof(CashVoucherUpdateRequest.EmployeeId));
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
        Assert.Contains(
            typeof(CashVoucherUpdateRequest).GetProperties(),
            property => property.Name ==
                nameof(CashVoucherUpdateRequest.EmployeeId));
        Assert.DoesNotContain(
            typeof(CashVoucherUpdateRequest).GetProperties(),
            property => property.Name == "PartyType");
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
                    CashMovementClassification.Other,
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
                null,
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

    private static CashMovementTypeRequest CreateMovementType(
        CashMovementClassification classification,
        bool forPartner) =>
        new(
            Name: "Movement",
            Direction: CashDirection.Receipt,
            Classification: classification,
            ForPartner: forPartner,
            IsActive: true,
            IsDefaultForSales: false,
            IsDefaultForPurchase: false,
            IsDefaultForSalesReturn: false,
            IsDefaultForPurchaseReturn: false,
            Notes: null);

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

    [Fact]
    public void CashVoucherBulkValidator_RejectsDuplicateIdsAndInvalidTypedItems()
    {
        var validator = new CashVoucherBulkRequestValidator();
        var result = validator.Validate(
            new CashVoucherBulkRequest(
            [
                new CashVoucherBulkAddItemRequest(
                    Voucher: null),
                new CashVoucherBulkUpdateItemRequest(
                    Id: 7,
                    RowVersion: new byte[8],
                    Voucher: CreateBulkVoucher()),
                new CashVoucherBulkDeleteItemRequest(
                    Id: 7,
                    RowVersion: new byte[8]),
                new CashVoucherBulkUpdateItemRequest(
                    Id: 0,
                    RowVersion: null,
                    Voucher: null)
            ]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CashVoucherBulkRequest.Items));
        Assert.Contains(
            result.Errors,
            error => error.PropertyName.EndsWith(
                nameof(CashVoucherBulkUpdateItemRequest.Id),
                StringComparison.Ordinal));
        Assert.Contains(
            result.Errors,
            error => error.PropertyName.EndsWith(
                nameof(CashVoucherBulkAddItemRequest.Voucher),
                StringComparison.Ordinal));
    }

    [Fact]
    public void CashVoucherBulkValidator_RejectsMoreThanMaximumItems()
    {
        var items = Enumerable.Range(
                0,
                CashVoucherBulkRequestValidator.MaximumItems + 1)
            .Select(index =>
                new CashVoucherBulkDeleteItemRequest(
                    Id: index + 1,
                    RowVersion: new byte[8]))
            .ToArray();

        var result = new CashVoucherBulkRequestValidator().Validate(
            new CashVoucherBulkRequest(items));

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CashVoucherBulkRequest.Items));
    }

    private static CashVoucherBulkVoucherRequest CreateBulkVoucher() =>
        new(
            VoucherDate: new DateOnly(2026, 7, 27),
            Direction: CashDirection.Receipt,
            CashboxId: 1,
            CashMovementTypeId: 3,
            EmployeeId: null,
            BusinessPartnerId: null,
            DriverId: null,
            DriverTripId: null,
            ExternalPartyName: null,
            Amount: 10m,
            ReferenceNumber: null,
            Description: null,
            Notes: null,
            ExchangeRate: null);

    private static CashVoucherUpdateRequest CreateVoucher(
        int? employeeId,
        int? partnerId,
        int? driverId,
        int? tripId,
        string? externalPartyName) =>
        new(
            new DateOnly(2026, 7, 27),
            CashDirection.Receipt,
            1,
            3,
            employeeId,
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
