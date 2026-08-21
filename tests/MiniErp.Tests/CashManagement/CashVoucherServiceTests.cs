using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
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

        var receipt = await AddVoucherAsync(vouchers,
            CreateRequest(
                "CV-RECEIPT",
                CashDirection.Receipt,
                movementTypeId: 3,
                amount: 200m));
        var payment = await AddVoucherAsync(vouchers,
            CreateRequest(
                "CV-PAYMENT",
                CashDirection.Payment,
                movementTypeId: 4,
                amount: 125m));
        var cashbox = await cashboxes.GetByIdAsync(1);

        Assert.True(receipt.IsSuccess);
        Assert.True(payment.IsSuccess);
        Assert.Equal(receipt.Value.Amount, receipt.Value.BaseAmount);
        Assert.Equal(1m, receipt.Value.ExchangeRate);
        Assert.Equal(payment.Value.Amount, payment.Value.BaseAmount);
        Assert.Equal(1m, payment.Value.ExchangeRate);
        Assert.Equal(1075m, cashbox.Value.CurrentBalance);
    }

    [Fact]
    public async Task ReceiptAndPaymentSupportForeignCurrencyCashbox()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var vouchers = database.CreateVoucherService(companyId: 1);

        var receipt = await AddVoucherAsync(
            vouchers,
            CreatePartnerRequest(
                "CV-USD-RECEIPT",
                CashDirection.Receipt,
                movementTypeId: 1,
                partnerId: 5,
                amount: 10m,
                cashboxId: 5,
                exchangeRate: 50m));
        var payment = await AddVoucherAsync(
            vouchers,
            CreatePartnerRequest(
                "CV-USD-PAYMENT",
                CashDirection.Payment,
                movementTypeId: 2,
                partnerId: 5,
                amount: 4m,
                cashboxId: 5,
                exchangeRate: 51m));
        var cashbox = await database.CreateCashboxService(1)
            .GetByIdAsync(5);

        Assert.True(receipt.IsSuccess);
        Assert.Equal(CurrencyCode.USD, receipt.Value.Currency);
        Assert.Equal(CurrencyCode.EGP, receipt.Value.BaseCurrency);
        Assert.Equal(50m, receipt.Value.ExchangeRate);
        Assert.Equal(500m, receipt.Value.BaseAmount);

        Assert.True(payment.IsSuccess);
        Assert.Equal(CurrencyCode.USD, payment.Value.Currency);
        Assert.Equal(CurrencyCode.EGP, payment.Value.BaseCurrency);
        Assert.Equal(51m, payment.Value.ExchangeRate);
        Assert.Equal(204m, payment.Value.BaseAmount);
        Assert.Equal(106m, cashbox.Value.CurrentBalance);
    }

    [Fact]
    public async Task InitialSaveCreatesDraftWithAutomaticNumberAndNoCashEffect()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 1),
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                Amount: 250m,
                Description: "Collected before posting details"));
        var cashbox = await database.CreateCashboxService(1)
            .GetByIdAsync(1);
        var drafts = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new CashVoucherFilterRequest(IsDraft: true));
        var completed = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new CashVoucherFilterRequest(IsDraft: false));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDraft);
        Assert.Matches("^RCV-[0-9]{4,}$", result.Value.VoucherNumber);
        Assert.Equal(1, result.Value.CashboxId);
        Assert.Null(result.Value.CashMovementTypeId);
        Assert.Equal(
            "Collected before posting details",
            result.Value.Description);
        Assert.Equal(1000m, cashbox.Value.CurrentBalance);
        Assert.Single(drafts.Value.Items);
        Assert.Empty(completed.Value.Items);
        Assert.Empty(await database.Context.BusinessPartnerMovements
            .Where(movement => movement.CashVoucherId == result.Value.Id)
            .ToListAsync());
    }

    [Fact]
    public async Task EditingDraftAddsPostingDetailsAndKeepsAutomaticNumber()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var created = await database.CreateVoucherService(1).AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 1),
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                Amount: 250m,
                Description: "Collected before posting details"));

        await using var updateContext = database.CreateAdditionalContext();
        var updated = await database.CreateVoucherService(1, updateContext)
            .UpdateAsync(
                created.Value.Id,
                new CashVoucherUpdateRequest(
                    created.Value.VoucherDate,
                    created.Value.Direction,
                    CashboxId: 1,
                    CashMovementTypeId: 3,
                    EmployeeId: null,
                    BusinessPartnerId: null,
                    DriverId: null,
                    DriverTripId: null,
                    ExternalPartyName: null,
                    Amount: created.Value.Amount,
                    ReferenceNumber: "POSTED",
                    Description: "Completed draft",
                    Notes: created.Value.Notes,
                    RowVersion: created.Value.RowVersion));
        var cashbox = await database.CreateCashboxService(1)
            .GetByIdAsync(1);

        Assert.True(updated.IsSuccess);
        Assert.False(updated.Value.IsDraft);
        Assert.Equal(created.Value.VoucherNumber, updated.Value.VoucherNumber);
        Assert.Equal(1, updated.Value.CashboxId);
        Assert.Equal(3, updated.Value.CashMovementTypeId);
        Assert.Equal(1250m, cashbox.Value.CurrentBalance);
    }

    [Fact]
    public async Task PaymentCannotMakeCashboxNegative()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await AddVoucherAsync(service,
            CreateRequest(
                "CV-TOO-LARGE",
                CashDirection.Payment,
                movementTypeId: 4,
                amount: 1001m));

        Assert.Equal(
            "CashVouchers.InsufficientCashboxBalance",
            result.Error.Code);
        Assert.Single(await database.Context.CashVouchers.ToListAsync());
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

        var result = await AddVoucherAsync(service,
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

        var mismatch = await AddVoucherAsync(service,
            CreateRequest(
                "CV-MISMATCH",
                CashDirection.Payment,
                movementTypeId: 3,
                amount: 10m));
        var crossCompanyCashbox = await AddVoucherAsync(service,
            CreateRequest(
                "CV-CROSS-BOX",
                CashDirection.Receipt,
                cashboxId: 4,
                movementTypeId: 3,
                amount: 10m));
        var crossCompanyPartner = await AddVoucherAsync(service,
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

        var partnerWithGeneralType = await AddVoucherAsync(service,
            CreatePartnerRequest(
                "CV-PARTNER-GENERAL-TYPE",
                CashDirection.Receipt,
                movementTypeId: 3,
                partnerId: 1,
                amount: 10m));
        var generalWithPartnerType = await AddVoucherAsync(service,
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

        var result = await AddVoucherAsync(service,
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

        var result = await AddVoucherAsync(service,
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
    public async Task EmployeeVoucherDerivesPartyAndMapsEmployeeFields()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 27),
                Direction: CashDirection.Payment,
                CashboxId: 1,
                Amount: 75m,
                Description: "Employee cash payment"));

        var result = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: 4,
                EmployeeId: 1,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 75m,
                ReferenceNumber: null,
                Description: "Employee cash payment",
                Notes: null,
                RowVersion: draft.Value.RowVersion));

        Assert.True(result.IsSuccess);
        Assert.Equal(CashPartyType.Employee, result.Value.PartyType);
        Assert.Equal(1, result.Value.EmployeeId);
        Assert.Equal("Employee One", result.Value.EmployeeName);
        Assert.Null(result.Value.BusinessPartnerId);
        Assert.Null(result.Value.DriverId);
        Assert.Empty(
            await database.Context.BusinessPartnerMovements
                .Where(item => item.CashVoucherId == result.Value.Id)
                .ToListAsync());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task EmployeeVoucherRejectsInactiveAndCrossCompanyEmployee(
        int employeeId)
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 27),
                Direction: CashDirection.Payment,
                CashboxId: 1,
                Amount: 25m,
                Description: "Employee validation"));

        var result = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: 4,
                EmployeeId: employeeId,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 25m,
                ReferenceNumber: null,
                Description: "Employee validation",
                Notes: null,
                RowVersion: draft.Value.RowVersion));

        Assert.Equal("CashVouchers.EmployeeNotFound", result.Error.Code);
    }

    [Fact]
    public async Task ExternalPartyNameStillDerivesOtherParty()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 27),
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                Amount: 50m,
                Description: "Historical external party"));

        var result = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                CashMovementTypeId: 3,
                EmployeeId: null,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: "External party",
                Amount: 50m,
                ReferenceNumber: null,
                Description: "Historical external party",
                Notes: null,
                RowVersion: draft.Value.RowVersion));

        Assert.True(result.IsSuccess);
        Assert.Equal(CashPartyType.Other, result.Value.PartyType);
        Assert.Equal("External party", result.Value.ExternalPartyName);
        Assert.Null(result.Value.EmployeeId);
    }

    [Fact]
    public async Task VoucherRejectsMultiplePartySources()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 27),
                Direction: CashDirection.Payment,
                CashboxId: 1,
                Amount: 25m,
                Description: "Invalid party selection"));

        var result = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: 4,
                EmployeeId: 1,
                BusinessPartnerId: null,
                DriverId: 1,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 25m,
                ReferenceNumber: null,
                Description: "Invalid party selection",
                Notes: null,
                RowVersion: draft.Value.RowVersion));

        Assert.Equal(
            "CashVouchers.PartySelectionMustBeExclusive",
            result.Error.Code);
    }

    [Fact]
    public async Task DriverVoucherRejectsTripOwnedByAnotherDriver()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await AddVoucherAsync(service,
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
        var created = await AddVoucherAsync(createService,
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
        var created = await AddVoucherAsync(createService,
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
                created.Value.VoucherDate,
                CashDirection.Payment,
                created.Value.CashboxId,
                4,
                null,
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
        var created = await AddVoucherAsync(service,
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
    public async Task AutomaticVoucherNumbersAreUnique()
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

        var first = await AddVoucherAsync(service, request);
        var second = await AddVoucherAsync(service, request);
        var cashbox = await database.CreateCashboxService(1).GetByIdAsync(1);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value.Id, second.Value.Id);
        Assert.Equal("RCV-0001", first.Value.VoucherNumber);
        Assert.Equal("RCV-0002", second.Value.VoucherNumber);
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
        var created = await AddVoucherAsync(
            database.CreateVoucherService(1),
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

    private static VoucherTestRequest CreateRequest(
        string number,
        CashDirection direction,
        int cashboxId = 1,
        int movementTypeId = 3,
        decimal amount = 10m) =>
        new(
            new DateOnly(2026, 7, 27),
            direction,
            cashboxId,
            movementTypeId,
            null,
            null,
            null,
            null,
            null,
            amount,
            null,
            null,
            null);

    private static VoucherTestRequest CreatePartnerRequest(
        string number,
        CashDirection direction,
        int movementTypeId,
        int partnerId,
        decimal amount,
        int cashboxId = 1,
        decimal? exchangeRate = null) =>
        new(
            new DateOnly(2026, 7, 27),
            direction,
            cashboxId,
            movementTypeId,
            null,
            partnerId,
            null,
            null,
            null,
            amount,
            null,
            null,
            null,
            exchangeRate);

    private static VoucherTestRequest CreateDriverRequest(
        string number,
        CashDirection direction,
        int driverId,
        int? tripId,
        decimal amount) =>
        new(
            new DateOnly(2026, 7, 27),
            direction,
            1,
            4,
            null,
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
            original.VoucherDate,
            original.Direction,
            cashboxId ?? original.CashboxId,
            original.CashMovementTypeId,
            original.EmployeeId,
            original.BusinessPartnerId,
            original.DriverId,
            original.DriverTripId,
            original.ExternalPartyName,
            amount ?? original.Amount,
            original.ReferenceNumber,
            original.Description,
            original.Notes,
            original.RowVersion);

    private static async Task<Result<CashVoucherResponse>> AddVoucherAsync(
        ICashVoucherService service,
        VoucherTestRequest request)
    {
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                request.VoucherDate,
                request.Direction,
                request.CashboxId,
                request.Amount,
                request.Description));
        if (draft.IsFailure)
        {
            return draft;
        }

        return await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                request.VoucherDate,
                request.Direction,
                request.CashboxId,
                request.CashMovementTypeId,
                request.EmployeeId,
                request.BusinessPartnerId,
                request.DriverId,
                request.DriverTripId,
                request.ExternalPartyName,
                request.Amount,
                request.ReferenceNumber,
                request.Description,
                request.Notes,
                draft.Value.RowVersion,
                request.ExchangeRate));
    }

    private sealed record VoucherTestRequest(
        DateOnly VoucherDate,
        CashDirection Direction,
        int CashboxId,
        int CashMovementTypeId,
        int? EmployeeId,
        int? BusinessPartnerId,
        int? DriverId,
        int? DriverTripId,
        string? ExternalPartyName,
        decimal Amount,
        string? ReferenceNumber,
        string? Description,
        string? Notes,
        decimal? ExchangeRate = null);
}
