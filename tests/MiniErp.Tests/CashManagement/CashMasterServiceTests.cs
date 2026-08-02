using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.CashMovementTypes;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.Cashboxes;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.CashManagement;

public sealed class CashMasterServiceTests
{
    static CashMasterServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task Cashbox_DuplicateRulesAreTenantScoped()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var companyOne = database.CreateCashboxService(companyId: 1);
        var companyTwo = database.CreateCashboxService(companyId: 2);

        var duplicateCode = await companyOne.AddAsync(
            CreateCashbox(" main ", "Different"));
        var duplicateName = await companyOne.AddAsync(
            CreateCashbox("THIRD", " main cashbox "));
        var sameCodeInOtherCompany = await companyTwo.AddAsync(
            CreateCashbox("SECOND", "Second Company Box"));

        Assert.Equal("Cashboxes.CodeExists", duplicateCode.Error.Code);
        Assert.Equal("Cashboxes.NameExists", duplicateName.Error.Code);
        Assert.True(sameCodeInOtherCompany.IsSuccess);
        Assert.Equal(2, sameCodeInOtherCompany.Value.CompanyId);
    }

    [Fact]
    public async Task Cashbox_GetAllCombinesSearchAndTypedFilters()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateCashboxService(companyId: 1);

        var result = await service.GetAllAsync(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20
            },
            new CashboxFilterRequest(
                Search: "Main",
                Currency: CurrencyCode.EGP,
                IsActive: true));

        Assert.True(result.IsSuccess);
        var cashbox = Assert.Single(result.Value.Items);
        Assert.Equal("MAIN", cashbox.Code);
        Assert.Equal(1000m, cashbox.CurrentBalance);
    }

    [Fact]
    public async Task Cashbox_WithVoucherBlocksOpeningBalanceChangeAndDelete()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var vouchers = database.CreateVoucherService(companyId: 1);
        var cashboxes = database.CreateCashboxService(companyId: 1);

        var created = await vouchers.AddAsync(
            CreateGeneralVoucher(
                "CV-MASTER-1",
                CashDirection.Receipt,
                cashboxId: 1,
                movementTypeId: 3,
                amount: 25m));
        var cashbox = await cashboxes.GetByIdAsync(1);

        var update = await cashboxes.UpdateAsync(
            1,
            new CashboxUpdateRequest(
                cashbox.Value.Code,
                cashbox.Value.Name,
                cashbox.Value.Currency,
                1200m,
                true,
                cashbox.Value.Notes,
                cashbox.Value.RowVersion));
        var delete = await cashboxes.DeleteAsync(1);

        Assert.True(created.IsSuccess);
        Assert.Equal(
            "Cashboxes.OpeningOrCurrencyChangeNotAllowed",
            update.Error.Code);
        Assert.Equal("Cashboxes.HasVouchers", delete.Error.Code);
    }

    [Fact]
    public async Task MovementType_DuplicateAndUsedSemanticsRulesAreEnforced()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var types = database.CreateMovementTypeService(companyId: 1);
        var vouchers = database.CreateVoucherService(companyId: 1);

        var duplicate = await types.AddAsync(
            new CashMovementTypeRequest(
                " customer collection ",
                CashDirection.Receipt,
                ForPartner: true,
                IsActive: true,
                IsDefaultForSales: false,
                IsDefaultForPurchase: false,
                IsDefaultForSalesReturn: false,
                IsDefaultForPurchaseReturn: false,
                Notes: null));
        var createdVoucher = await vouchers.AddAsync(
            CreateGeneralVoucher(
                "CV-TYPE-1",
                CashDirection.Receipt,
                cashboxId: 1,
                movementTypeId: 3,
                amount: 10m));
        var movementType = await types.GetByIdAsync(3);
        var semanticChange = await types.UpdateAsync(
            3,
            new CashMovementTypeUpdateRequest(
                movementType.Value.Name,
                CashDirection.Payment,
                ForPartner: false,
                IsActive: true,
                IsDefaultForSales: false,
                IsDefaultForPurchase: false,
                IsDefaultForSalesReturn: false,
                IsDefaultForPurchaseReturn: false,
                Notes: movementType.Value.Notes,
                RowVersion: movementType.Value.RowVersion));
        var delete = await types.DeleteAsync(3);

        Assert.Equal("CashMovementTypes.NameExists", duplicate.Error.Code);
        Assert.True(createdVoucher.IsSuccess);
        Assert.Equal(
            "CashMovementTypes.UsedSemanticsChangeNotAllowed",
            semanticChange.Error.Code);
        Assert.Equal("CashMovementTypes.HasVouchers", delete.Error.Code);
    }

    [Fact]
    public async Task MovementType_SelectFiltersDirectionAndPartnerUse()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateMovementTypeService(companyId: 1);

        var partnerReceipts = await service.GetSelectAsync(
            new CashMovementTypeSelectRequest(
                CashDirection.Receipt,
                ForPartner: true));
        var generalPayments = await service.GetSelectAsync(
            new CashMovementTypeSelectRequest(
                CashDirection.Payment,
                ForPartner: false));

        Assert.All(
            partnerReceipts.Value,
            item =>
            {
                Assert.Equal(CashDirection.Receipt, item.Direction);
            });
        Assert.Contains(
            generalPayments.Value,
            item => item.Name == "Driver Advance");
        Assert.DoesNotContain(
            generalPayments.Value,
            item => item.Name == "Inactive Payment");
    }

    [Fact]
    public async Task MovementType_NewInvoiceDefaultReplacesExactInvoiceTypeOnly()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateMovementTypeService(companyId: 1);

        var created = await service.AddAsync(
            new CashMovementTypeRequest(
                "Alternative Collection",
                CashDirection.Receipt,
                ForPartner: true,
                IsActive: true,
                IsDefaultForSales: true,
                IsDefaultForPurchase: false,
                IsDefaultForSalesReturn: false,
                IsDefaultForPurchaseReturn: false,
                Notes: null));
        var defaults = await database.Context.CashMovementTypes
            .Where(movementType =>
                movementType.IsDefaultForSales ||
                movementType.IsDefaultForPurchase ||
                movementType.IsDefaultForSalesReturn ||
                movementType.IsDefaultForPurchaseReturn)
            .OrderBy(movementType => movementType.Id)
            .Select(movementType => new
            {
                movementType.Id,
                movementType.IsDefaultForSales,
                movementType.IsDefaultForPurchase,
                movementType.IsDefaultForSalesReturn,
                movementType.IsDefaultForPurchaseReturn
            })
            .ToListAsync();

        Assert.True(created.IsSuccess);
        Assert.Equal(4, defaults.Count);
        Assert.Contains(defaults, item =>
            item.Id == created.Value.Id &&
            item.IsDefaultForSales);
        Assert.Contains(defaults, item =>
            item.Id == 2 &&
            item.IsDefaultForPurchase);
        Assert.Contains(defaults, item =>
            item.Id == 7 &&
            item.IsDefaultForPurchaseReturn);
        Assert.Contains(defaults, item =>
            item.Id == 8 &&
            item.IsDefaultForSalesReturn);
        Assert.DoesNotContain(defaults, item => item.Id == 1);
    }

    [Theory]
    [InlineData(
        CashDirection.Receipt,
        true,
        PartnerAccountEffect.Credit)]
    [InlineData(
        CashDirection.Payment,
        true,
        PartnerAccountEffect.Debit)]
    [InlineData(
        CashDirection.Receipt,
        false,
        PartnerAccountEffect.None)]
    [InlineData(
        CashDirection.Payment,
        false,
        PartnerAccountEffect.None)]
    public async Task MovementType_DerivesPartnerEffect(
        CashDirection direction,
        bool forPartner,
        PartnerAccountEffect expectedEffect)
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateMovementTypeService(companyId: 1);

        var result = await service.AddAsync(
            new CashMovementTypeRequest(
                $"Derived {direction} {forPartner}",
                direction,
                forPartner,
                IsActive: true,
                IsDefaultForSales: false,
                IsDefaultForPurchase: false,
                IsDefaultForSalesReturn: false,
                IsDefaultForPurchaseReturn: false,
                Notes: null));
        var storedEffect = await database.Context.CashMovementTypes
            .Where(entity => entity.Id == result.Value.Id)
            .Select(entity => entity.PartnerEffect)
            .SingleAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(forPartner, result.Value.ForPartner);
        Assert.Equal(expectedEffect, storedEffect);
    }

    private static CashboxRequest CreateCashbox(
        string code,
        string name) =>
        new(
            code,
            name,
            CurrencyCode.EGP,
            0m,
            true,
            null);

    private static CashVoucherRequest CreateGeneralVoucher(
        string number,
        CashDirection direction,
        int cashboxId,
        int movementTypeId,
        decimal amount) =>
        new(
            number,
            new DateOnly(2026, 7, 27),
            direction,
            cashboxId,
            movementTypeId,
            CashPartyType.None,
            null,
            null,
            null,
            null,
            amount,
            null,
            null,
            null);
}
