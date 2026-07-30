using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.CashManagement;

public sealed class CashVoucherServiceTests
{
    static CashVoucherServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task ReceiptAndPaymentChangeOnlyDerivedCashboxBalance()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var vouchers = database.CreateVoucherService(companyId: 1);
        var cashboxes = database.CreateCashboxService(companyId: 1);

        var receipt = await vouchers.AddAsync(
            CreateRequest(
                "CV-RECEIPT",
                CashDirection.Receipt,
                movementTypeId: 3,
                amount: 200m));
        var payment = await vouchers.AddAsync(
            CreateRequest(
                "CV-PAYMENT",
                CashDirection.Payment,
                movementTypeId: 4,
                amount: 125m));
        var cashbox = await cashboxes.GetByIdAsync(1);

        Assert.True(receipt.IsSuccess);
        Assert.True(payment.IsSuccess);
        Assert.Equal(1075m, cashbox.Value.CurrentBalance);
    }

    [Fact]
    public async Task PaymentCannotMakeCashboxNegative()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await service.AddAsync(
            CreateRequest(
                "CV-TOO-LARGE",
                CashDirection.Payment,
                movementTypeId: 4,
                amount: 1001m));

        Assert.Equal(
            "CashVouchers.InsufficientCashboxBalance",
            result.Error.Code);
        Assert.Equal(0, await database.Context.CashVouchers.CountAsync());
    }

    [Theory]
    [InlineData(3, 3, "CashVouchers.CashboxInactive")]
    [InlineData(1, 5, "CashVouchers.MovementTypeInactive")]
    public async Task NewVoucherRejectsInactiveCashMaster(
        int cashboxId,
        int movementTypeId,
        string expectedCode)
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await service.AddAsync(
            CreateRequest(
                "CV-INACTIVE",
                CashDirection.Payment,
                cashboxId,
                movementTypeId,
                10m));

        Assert.Equal(expectedCode, result.Error.Code);
    }

    [Fact]
    public async Task VoucherRejectsDirectionMismatchAndCrossCompanyReferences()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var mismatch = await service.AddAsync(
            CreateRequest(
                "CV-MISMATCH",
                CashDirection.Payment,
                movementTypeId: 3,
                amount: 10m));
        var crossCompanyCashbox = await service.AddAsync(
            CreateRequest(
                "CV-CROSS-BOX",
                CashDirection.Receipt,
                cashboxId: 4,
                movementTypeId: 3,
                amount: 10m));
        var crossCompanyPartner = await service.AddAsync(
            CreatePartnerRequest(
                "CV-CROSS-PARTNER",
                CashDirection.Receipt,
                movementTypeId: 1,
                partnerId: 3,
                amount: 10m));

        Assert.Equal(
            "CashVouchers.MovementTypeDirectionMismatch",
            mismatch.Error.Code);
        Assert.Equal(
            "CashVouchers.CashboxNotFound",
            crossCompanyCashbox.Error.Code);
        Assert.Equal(
            "CashVouchers.PartnerNotFound",
            crossCompanyPartner.Error.Code);
    }

    [Fact]
    public async Task VoucherRequiresMovementTypeMatchingPartyUsage()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var partnerWithGeneralType = await service.AddAsync(
            CreatePartnerRequest(
                "CV-PARTNER-GENERAL-TYPE",
                CashDirection.Receipt,
                movementTypeId: 3,
                partnerId: 1,
                amount: 10m));
        var generalWithPartnerType = await service.AddAsync(
            CreateRequest(
                "CV-GENERAL-PARTNER-TYPE",
                CashDirection.Receipt,
                movementTypeId: 1,
                amount: 10m));

        Assert.Equal(
            "CashVouchers.MovementTypeNotForPartner",
            partnerWithGeneralType.Error.Code);
        Assert.Equal(
            "CashVouchers.MovementTypeForPartnerOnly",
            generalWithPartnerType.Error.Code);
    }

    [Theory]
    [InlineData(
        CashDirection.Receipt,
        1,
        BusinessPartnerMovementType.CashReceipt,
        0,
        150)]
    [InlineData(
        CashDirection.Payment,
        2,
        BusinessPartnerMovementType.CashPayment,
        150,
        0)]
    public async Task PartnerVoucherCreatesExactlyOneConfiguredMovement(
        CashDirection direction,
        int movementTypeId,
        BusinessPartnerMovementType expectedType,
        decimal expectedDebit,
        decimal expectedCredit)
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await service.AddAsync(
            CreatePartnerRequest(
                $"CV-PARTNER-{direction}",
                direction,
                movementTypeId,
                partnerId: 1,
                amount: 150m));
        var movement = await database.Context.BusinessPartnerMovements
            .SingleAsync(item => item.CashVoucherId == result.Value.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedType, movement.MovementType);
        Assert.Equal(expectedDebit, movement.Debit);
        Assert.Equal(expectedCredit, movement.Credit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    public async Task DriverVoucherSupportsOptionalMatchingTrip(int? tripId)
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await service.AddAsync(
            CreateDriverRequest(
                $"CV-DRIVER-{tripId}",
                CashDirection.Payment,
                driverId: 1,
                tripId,
                amount: 75m));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.DriverId);
        Assert.Equal(tripId, result.Value.DriverTripId);
        Assert.Empty(
            await database.Context.BusinessPartnerMovements
                .Where(item => item.CashVoucherId == result.Value.Id)
                .ToListAsync());
    }

    [Fact]
    public async Task DriverVoucherRejectsTripOwnedByAnotherDriver()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await service.AddAsync(
            CreateDriverRequest(
                "CV-WRONG-TRIP",
                CashDirection.Payment,
                driverId: 1,
                tripId: 2,
                amount: 10m));

        Assert.Equal("CashVouchers.DriverTripNotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateMovesFullEffectBetweenCashboxesWithoutDuplication()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var createService = database.CreateVoucherService(companyId: 1);
        var created = await createService.AddAsync(
            CreateRequest(
                "CV-MOVE",
                CashDirection.Payment,
                movementTypeId: 4,
                amount: 100m));

        await using var updateContext = database.CreateAdditionalContext();
        var updateService = database.CreateVoucherService(
            companyId: 1,
            updateContext);
        var updated = await updateService.UpdateAsync(
            created.Value.Id,
            ToUpdateRequest(
                created.Value,
                cashboxId: 2,
                amount: 150m));
        var cashboxes = database.CreateCashboxService(companyId: 1);
        var first = await cashboxes.GetByIdAsync(1);
        var second = await cashboxes.GetByIdAsync(2);

        Assert.True(updated.IsSuccess);
        Assert.Equal(1000m, first.Value.CurrentBalance);
        Assert.Equal(350m, second.Value.CurrentBalance);
        Assert.False(created.Value.RowVersion.SequenceEqual(
            updated.Value.RowVersion));
    }

    [Fact]
    public async Task UpdateFromPartnerToDriverRemovesPartnerEffect()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var createService = database.CreateVoucherService(companyId: 1);
        var created = await createService.AddAsync(
            CreatePartnerRequest(
                "CV-CHANGE-PARTY",
                CashDirection.Payment,
                movementTypeId: 2,
                partnerId: 1,
                amount: 100m));

        await using var updateContext = database.CreateAdditionalContext();
        var updateService = database.CreateVoucherService(
            companyId: 1,
            updateContext);
        var updated = await updateService.UpdateAsync(
            created.Value.Id,
            new CashVoucherUpdateRequest(
                created.Value.VoucherNumber,
                created.Value.VoucherDate,
                CashDirection.Payment,
                created.Value.CashboxId,
                4,
                CashPartyType.Driver,
                null,
                1,
                null,
                null,
                100m,
                null,
                "Driver advance",
                null,
                created.Value.RowVersion));

        Assert.True(updated.IsSuccess);
        Assert.Equal(CashPartyType.Driver, updated.Value.PartyType);
        Assert.Empty(
            await database.Context.BusinessPartnerMovements
                .Where(item => item.CashVoucherId == created.Value.Id)
                .ToListAsync());
        Assert.Single(
            await database.Context.BusinessPartnerMovements
                .IgnoreQueryFilters()
                .Where(item => item.CashVoucherId == created.Value.Id)
                .ToListAsync());
    }

    [Fact]
    public async Task DeleteReversesCashAndSoftDeletesPartnerMovement()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var created = await service.AddAsync(
            CreatePartnerRequest(
                "CV-DELETE",
                CashDirection.Receipt,
                movementTypeId: 1,
                partnerId: 1,
                amount: 80m));

        database.Context.ChangeTracker.Clear();
        var deleted = await service.DeleteAsync(created.Value.Id);
        var cashbox = await database.CreateCashboxService(1).GetByIdAsync(1);

        Assert.True(deleted.IsSuccess);
        Assert.Equal(1000m, cashbox.Value.CurrentBalance);
        Assert.Empty(await database.Context.CashVouchers.ToListAsync());
        Assert.Empty(
            await database.Context.BusinessPartnerMovements
                .Where(item => item.CashVoucherId == created.Value.Id)
                .ToListAsync());
        Assert.True(
            await database.Context.CashVouchers
                .IgnoreQueryFilters()
                .Where(item => item.Id == created.Value.Id)
                .Select(item => item.IsDeleted)
                .SingleAsync());
    }

    [Fact]
    public async Task InvoiceGeneratedVoucherIsVisibleButCannotBeChangedDirectly()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO CashVouchers (
                Id, CompanyId, InvoiceId, VoucherNumber, VoucherDate,
                Direction, CashboxId, CashMovementTypeId, PartyType,
                BusinessPartnerId, Amount, Currency, LastModifiedAt,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (
                100, 1, 1, 'INV-PAY-1', '2026-07-25',
                1, 1, 1, 2,
                1, 25, 1, '2026-07-25',
                'test', '2026-07-25', 'test', 0);
            """);
        var service = database.CreateVoucherService(companyId: 1);

        var generated = await service.GetByIdAsync(100);
        var update = await service.UpdateAsync(
            100,
            ToUpdateRequest(generated.Value, amount: 30m));
        var delete = await service.DeleteAsync(100);

        Assert.True(generated.IsSuccess);
        Assert.Equal(1, generated.Value.InvoiceId);
        Assert.Equal("INV-1", generated.Value.InvoiceNumber);
        Assert.Equal(
            "CashVouchers.InvoiceGeneratedReadOnly",
            update.Error.Code);
        Assert.Equal(
            "CashVouchers.InvoiceGeneratedReadOnly",
            delete.Error.Code);
        Assert.Equal(
            25m,
            await database.Context.CashVouchers
                .Where(voucher => voucher.Id == 100)
                .Select(voucher => voucher.Amount)
                .SingleAsync());
    }

    [Fact]
    public async Task DuplicateVoucherNumbersAreAllowed()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var request = CreatePartnerRequest(
            "CV-RETRY",
            CashDirection.Receipt,
            movementTypeId: 1,
            partnerId: 1,
            amount: 20m);

        var first = await service.AddAsync(request);
        var second = await service.AddAsync(request);
        var cashbox = await database.CreateCashboxService(1).GetByIdAsync(1);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value.Id, second.Value.Id);
        Assert.Equal(first.Value.VoucherNumber, second.Value.VoucherNumber);
        Assert.Equal(1040m, cashbox.Value.CurrentBalance);
        Assert.Equal(
            2,
            await database.Context.BusinessPartnerMovements.CountAsync(
                item =>
                    item.CashVoucherId == first.Value.Id ||
                    item.CashVoucherId == second.Value.Id));
    }

    [Fact]
    public async Task StaleRowVersionRejectsUpdateAndPreservesWinner()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var created = await database.CreateVoucherService(1).AddAsync(
            CreateRequest(
                "CV-CONCURRENCY",
                CashDirection.Receipt,
                movementTypeId: 3,
                amount: 10m));
        var original = created.Value;

        await using var winnerContext = database.CreateAdditionalContext();
        await using var staleContext = database.CreateAdditionalContext();
        var winnerService = database.CreateVoucherService(1, winnerContext);
        var staleService = database.CreateVoucherService(1, staleContext);

        var winner = await winnerService.UpdateAsync(
            original.Id,
            ToUpdateRequest(original, amount: 20m));
        var stale = await staleService.UpdateAsync(
            original.Id,
            ToUpdateRequest(original, amount: 30m));

        Assert.True(winner.IsSuccess);
        Assert.Equal("CashVouchers.Concurrency", stale.Error.Code);
        var persisted = await database.CreateVoucherService(1)
            .GetByIdAsync(original.Id);
        Assert.Equal(20m, persisted.Value.Amount);
    }

    private static CashVoucherRequest CreateRequest(
        string number,
        CashDirection direction,
        int cashboxId = 1,
        int movementTypeId = 3,
        decimal amount = 10m) =>
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

    private static CashVoucherRequest CreatePartnerRequest(
        string number,
        CashDirection direction,
        int movementTypeId,
        int partnerId,
        decimal amount) =>
        new(
            number,
            new DateOnly(2026, 7, 27),
            direction,
            1,
            movementTypeId,
            CashPartyType.Partner,
            partnerId,
            null,
            null,
            null,
            amount,
            null,
            null,
            null);

    private static CashVoucherRequest CreateDriverRequest(
        string number,
        CashDirection direction,
        int driverId,
        int? tripId,
        decimal amount) =>
        new(
            number,
            new DateOnly(2026, 7, 27),
            direction,
            1,
            4,
            CashPartyType.Driver,
            null,
            driverId,
            tripId,
            null,
            amount,
            null,
            null,
            null);

    private static CashVoucherUpdateRequest ToUpdateRequest(
        CashVoucherResponse original,
        int? cashboxId = null,
        decimal? amount = null) =>
        new(
            original.VoucherNumber,
            original.VoucherDate,
            original.Direction,
            cashboxId ?? original.CashboxId,
            original.CashMovementTypeId,
            original.PartyType,
            original.BusinessPartnerId,
            original.DriverId,
            original.DriverTripId,
            original.ExternalPartyName,
            amount ?? original.Amount,
            original.ReferenceNumber,
            original.Description,
            original.Notes,
            original.RowVersion);
}
