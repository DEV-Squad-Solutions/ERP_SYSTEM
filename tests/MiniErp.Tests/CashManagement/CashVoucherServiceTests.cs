using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Tests.TestDoubles;

namespace MiniErp.Tests.CashManagement;

public sealed class CashVoucherServiceTests
{
    static CashVoucherServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task PartySelect_ReturnsActiveCompanyPartiesOrderedByNameAndId()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE Drivers
            SET LicenseExpiryDate = '2020-01-01'
            WHERE Id = 1;

            INSERT INTO Drivers (
                Id, CompanyId, Code, Name, LicenseNumber,
                LicenseExpiryDate, IsActive,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (4, 1, 'DRV-4', 'Inactive Driver', 'LIC-4',
                 NULL, 0,
                 'test', '2026-01-01', 'test', 0),
                (5, 1, 'DRV-5', 'A Driver With Expired License', 'LIC-5',
                 '2020-01-01', 1,
                 'test', '2026-01-01', 'test', 0);

            INSERT INTO CashMovementTypes (
                Id, CompanyId, Name, Direction, Classification, PartnerEffect,
                IsActive, IsDefaultForSales, IsDefaultForPurchase,
                IsDefaultForSalesReturn, IsDefaultForPurchaseReturn,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (101, 1, 'Advertising Expense', 2, 2, 0,
                 1, 0, 0, 0, 0, 'test', '2026-01-01', 'test', 0),
                (102, 1, 'Office Expense', 1, 2, 0,
                 1, 0, 0, 0, 0, 'test', '2026-01-01', 'test', 0),
                (103, 1, 'Advertising Revenue', 1, 3, 0,
                 1, 0, 0, 0, 0, 'test', '2026-01-01', 'test', 0),
                (104, 1, 'Consulting Revenue', 2, 3, 0,
                 1, 0, 0, 0, 0, 'test', '2026-01-01', 'test', 0),
                (105, 1, 'Inactive Expense', 2, 2, 0,
                 0, 0, 0, 0, 0, 'test', '2026-01-01', 'test', 0),
                (106, 2, 'Other Company Revenue', 1, 3, 0,
                 1, 0, 0, 0, 0, 'test', '2026-01-01', 'test', 0),
                (107, 1, 'Partner Revenue', 1, 3, 2,
                 1, 0, 0, 0, 0, 'test', '2026-01-01', 'test', 0);
            """);
        database.Context.ChangeTracker.Clear();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await service.GetPartySelectAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [1, 2, 5],
            result.Value.BusinessPartners.Select(item => item.Id));
        Assert.Equal(
            ["Customer One", "Supplier One", "USD Partner"],
            result.Value.BusinessPartners.Select(item => item.Name));
        Assert.Equal(
            [5, 1, 2],
            result.Value.Drivers.Select(item => item.Id));
        Assert.Equal(
            [
                "A Driver With Expired License",
                "Driver One",
                "Driver Two"
            ],
            result.Value.Drivers.Select(item => item.Name));
        Assert.Equal(
            [1],
            result.Value.Employees.Select(item => item.Id));
        Assert.Equal(
            ["Employee One"],
            result.Value.Employees.Select(item => item.Name));
        Assert.Equal(
            [2],
            result.Value.Expenses.Select(item => item.Id));
        Assert.Equal(
            ["Operating Expenses"],
            result.Value.Expenses.Select(item => item.Name));
        Assert.All(
            result.Value.Expenses,
            item => Assert.Equal(
                CashMovementClassification.Expense,
                item.Classification));
        Assert.Equal(
            [1],
            result.Value.Revenues.Select(item => item.Id));
        Assert.Equal(
            ["Other Revenue"],
            result.Value.Revenues.Select(item => item.Name));
        Assert.All(
            result.Value.Revenues,
            item => Assert.Equal(
                CashMovementClassification.Revenue,
                item.Classification));
        var movementIds = result.Value.Expenses
            .Concat(result.Value.Revenues)
            .Select(item => item.Id);
        Assert.DoesNotContain(102, movementIds);
        Assert.DoesNotContain(104, movementIds);
        Assert.DoesNotContain(105, movementIds);
        Assert.DoesNotContain(106, movementIds);
        Assert.DoesNotContain(107, movementIds);
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
                movementTypeId: 9,
                amount: 200m));
        var payment = await AddVoucherAsync(vouchers,
            CreateRequest(
                "CV-PAYMENT",
                CashDirection.Payment,
                movementTypeId: 10,
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
    public async Task Voucher_ListFiltersAndReturnsDerivedClassification()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var revenue = await AddVoucherAsync(service,
            CreateRequest(
                "CV-REVENUE",
                CashDirection.Receipt,
                movementTypeId: 9,
                amount: 20m));
        var settlement = await AddVoucherAsync(service,
            CreatePartnerRequest(
                "CV-SETTLEMENT",
                CashDirection.Receipt,
                partnerId: 1,
                amount: 10m));
        var result = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new CashVoucherFilterRequest(
                Classification: CashMovementClassification.Revenue));

        Assert.True(revenue.IsSuccess);
        Assert.True(settlement.IsSuccess);
        var voucher = Assert.Single(result.Value.Items);
        Assert.Equal(revenue.Value.Id, voucher.Id);
        Assert.Equal(CashMovementClassification.Revenue,
            voucher.Classification);
    }

    [Fact]
    public async Task Voucher_AccountFilterCanIncludeExpenseDescendants()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Accounts (
                Id, CompanyId, Code, Name, ParentAccountId, AccountType,
                NormalBalance, IsPosting, IsActive,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (200, 1, '5210', 'Office Expense - Child', 2, 5, 1, 1, 1,
                 'test', '2026-01-01', 'test', 0),
                (201, 1, '5211', 'Office Expense - Grandchild', 200, 5, 1, 1,
                 1, 'test', '2026-01-01', 'test', 0),
                (202, 1, '5220', 'Office Expense - Sibling', 2, 5, 1, 1, 1,
                 'test', '2026-01-01', 'test', 0),
                (203, 1, '5300', 'Unrelated Expense', NULL, 5, 1, 1, 1,
                 'test', '2026-01-01', 'test', 0);
            """);
        database.Context.ChangeTracker.Clear();
        var service = database.CreateVoucherService(companyId: 1);

        var parentVoucher = await AddVoucherAsync(
            service,
            CreateRequest(
                "CV-EXP-PARENT",
                CashDirection.Payment,
                amount: 10m,
                accountId: 2));
        var childVoucher = await AddVoucherAsync(
            service,
            CreateRequest(
                "CV-EXP-CHILD",
                CashDirection.Payment,
                amount: 10m,
                accountId: 200));
        var grandchildVoucher = await AddVoucherAsync(
            service,
            CreateRequest(
                "CV-EXP-GRANDCHILD",
                CashDirection.Payment,
                amount: 10m,
                accountId: 201));
        var siblingVoucher = await AddVoucherAsync(
            service,
            CreateRequest(
                "CV-EXP-SIBLING",
                CashDirection.Payment,
                amount: 10m,
                accountId: 202));
        var unrelatedVoucher = await AddVoucherAsync(
            service,
            CreateRequest(
                "CV-EXP-UNRELATED",
                CashDirection.Payment,
                amount: 10m,
                accountId: 203));

        Assert.True(parentVoucher.IsSuccess);
        Assert.True(childVoucher.IsSuccess);
        Assert.True(grandchildVoucher.IsSuccess);
        Assert.True(siblingVoucher.IsSuccess);
        Assert.True(unrelatedVoucher.IsSuccess);

        var descendants = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new CashVoucherFilterRequest(
                Direction: CashDirection.Payment,
                Classification: CashMovementClassification.Expense,
                AccountId: 2,
                IncludeSubAccounts: true));
        var exact = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new CashVoucherFilterRequest(
                Direction: CashDirection.Payment,
                Classification: CashMovementClassification.Expense,
                AccountId: 2));
        var childDescendants = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new CashVoucherFilterRequest(
                Direction: CashDirection.Payment,
                Classification: CashMovementClassification.Expense,
                AccountId: 200,
                IncludeSubAccounts: true));

        Assert.True(descendants.IsSuccess);
        Assert.True(exact.IsSuccess);
        Assert.True(childDescendants.IsSuccess);
        Assert.Equal(
            new[]
            {
                parentVoucher.Value.Id,
                childVoucher.Value.Id,
                grandchildVoucher.Value.Id,
                siblingVoucher.Value.Id
            }.OrderBy(id => id),
            descendants.Value.Items
                .Select(item => item.Id)
                .OrderBy(id => id));
        Assert.Equal(
            [parentVoucher.Value.Id],
            exact.Value.Items.Select(item => item.Id));
        Assert.Equal(
            new[]
            {
                childVoucher.Value.Id,
                grandchildVoucher.Value.Id
            }.OrderBy(id => id),
            childDescendants.Value.Items
                .Select(item => item.Id)
                .OrderBy(id => id));
        Assert.DoesNotContain(
            unrelatedVoucher.Value.Id,
            descendants.Value.Items.Select(item => item.Id));
        Assert.All(
            descendants.Value.Items,
            item =>
            {
                Assert.Equal(CashDirection.Payment, item.Direction);
                Assert.Equal(
                    CashMovementClassification.Expense,
                    item.Classification);
            });
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
                partnerId: 5,
                amount: 10m,
                cashboxId: 5,
                exchangeRate: 50m));
        var payment = await AddVoucherAsync(
            vouchers,
            CreatePartnerRequest(
                "CV-USD-PAYMENT",
                CashDirection.Payment,
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
    public async Task InitialSaveCreatesHandoverDraftWithoutAccountingOrCashEffect()
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
        var stored = await database.Context.CashVouchers
            .AsNoTracking()
            .SingleAsync(voucher => voucher.Id == result.Value.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDraft);
        Assert.False(stored.IsPosted);
        Assert.Matches("^RCV-[0-9]{4,}$", result.Value.VoucherNumber);
        Assert.Equal(1, result.Value.CashboxId);
        Assert.Null(result.Value.CashMovementTypeId);
        Assert.Null(result.Value.Classification);
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
    public async Task PaymentHandoverDraftMayExceedCashboxBalanceWithoutFinancialEffect()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 1),
                Direction: CashDirection.Payment,
                CashboxId: 1,
                Amount: 1_500m,
                Description: "Cash handover count",
                Notes: "Awaiting approval"));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.True(result.Value.IsDraft);
        Assert.Equal(CashPartyType.None, result.Value.PartyType);
        Assert.Null(result.Value.AccountId);
        Assert.Null(result.Value.CashMovementTypeId);
        Assert.Null(result.Value.EmployeeId);
        Assert.Null(result.Value.BusinessPartnerId);
        Assert.Null(result.Value.DriverId);
        Assert.Equal(1_500m, result.Value.Amount);
        var cashbox = await database.CreateCashboxService(1)
            .GetByIdAsync(1);
        Assert.Equal(1_000m, cashbox.Value.CurrentBalance);
        Assert.Empty(await database.Context.JournalEntries
            .Where(entry =>
                entry.SourceType == JournalEntrySourceType.CashVoucher &&
                entry.SourceId == result.Value.Id)
            .ToListAsync());
        Assert.Empty(await database.Context.BusinessPartnerMovements
            .Where(movement => movement.CashVoucherId == result.Value.Id)
            .ToListAsync());
        Assert.Empty(await database.Context.EmployeeMovements
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
                    CashMovementTypeId: 9,
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
        Assert.Equal(9, updated.Value.CashMovementTypeId);
        Assert.Equal(1250m, cashbox.Value.CurrentBalance);
    }

    [Fact]
    public async Task CompletingDraftRollsBackWhenJournalSynchronizationFails()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var postingService = new NoOpCashVoucherPostingService
        {
            FailSynchronization = true
        };
        var service = database.CreateVoucherService(
            companyId: 1,
            postingService: postingService);

        var result = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 21),
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                Amount: 250m,
                 Description: "Atomic draft posting"));

        await using var updateContext = database.CreateAdditionalContext();
        var updateService = database.CreateVoucherService(
            companyId: 1,
            context: updateContext,
            postingService: postingService);
        var completion = await updateService.UpdateAsync(
            result.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: result.Value.VoucherDate,
                Direction: result.Value.Direction,
                CashboxId: 1,
                CashMovementTypeId: null,
                EmployeeId: null,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: result.Value.Amount,
                ReferenceNumber: null,
                Description: result.Value.Description,
                Notes: result.Value.Notes,
                RowVersion: result.Value.RowVersion,
                AccountId: 1));

        Assert.True(completion.IsFailure);
        Assert.Equal("Tests.CashVoucherPostingFailed", completion.Error.Code);
        Assert.Single(postingService.SynchronizedVoucherIds);
        var stored = await database.Context.CashVouchers
            .AsNoTracking()
            .SingleAsync(voucher => voucher.Id == result.Value.Id);
        Assert.False(stored.IsPosted);
        Assert.Empty(await database.Context.JournalEntries
            .Where(entry => entry.SourceType == JournalEntrySourceType.CashVoucher &&
                            entry.SourceId == result.Value.Id)
            .ToListAsync());
    }

    [Fact]
    public async Task EditingDraftWithExpenseOrRevenueAccountPostsAndKeepsDescriptorOptional()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var created = await database.CreateVoucherService(1).AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 3),
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                Amount: 75m,
                Description: "Account-targeted receipt",
                AccountId: 1));

        await using var updateContext = database.CreateAdditionalContext();
        var updated = await database.CreateVoucherService(1, updateContext)
            .UpdateAsync(
                created.Value.Id,
                new CashVoucherUpdateRequest(
                    VoucherDate: created.Value.VoucherDate,
                    Direction: created.Value.Direction,
                    CashboxId: 1,
                    CashMovementTypeId: 9,
                    EmployeeId: null,
                    BusinessPartnerId: null,
                    DriverId: null,
                    DriverTripId: null,
                    ExternalPartyName: null,
                    Amount: created.Value.Amount,
                    ReferenceNumber: null,
                    Description: created.Value.Description,
                    Notes: null,
                    RowVersion: created.Value.RowVersion,
                    AccountId: 1));

        Assert.True(updated.IsSuccess);
        Assert.False(updated.Value.IsDraft);
        Assert.Equal(1, updated.Value.AccountId);
        Assert.Equal("4200", updated.Value.AccountCode);
        Assert.Equal("Other Revenue", updated.Value.AccountName);
        Assert.Equal(AccountType.Revenue, updated.Value.AccountType);
        Assert.Equal(9, updated.Value.CashMovementTypeId);
        Assert.Equal(75m, updated.Value.BaseAmount);
    }

    [Fact]
    public async Task UpdateWithoutAnyPostingTargetIsRejectedAndRemainsDraft()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 2),
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                Amount: 40m,
                 Description: "Post without category"));

        var updated = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: draft.Value.Direction,
                CashboxId: draft.Value.CashboxId,
                CashMovementTypeId: null,
                EmployeeId: null,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: draft.Value.Amount,
                ReferenceNumber: null,
                Description: draft.Value.Description,
                Notes: null,
                RowVersion: draft.Value.RowVersion));
        var cashbox = await database.CreateCashboxService(1)
            .GetByIdAsync(1);
        var stored = await database.Context.CashVouchers
            .AsNoTracking()
            .SingleAsync(voucher => voucher.Id == draft.Value.Id);

        Assert.True(updated.IsFailure);
        Assert.Equal(
            "CashVouchers.PartySelectionMustBeExclusive",
            updated.Error.Code);
        Assert.False(stored.IsPosted);
        Assert.Equal(1000m, cashbox.Value.CurrentBalance);
    }

    [Fact]
    public async Task PartyTargetWithNullMovementTypeRemainsPosted()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var original = await AddVoucherAsync(
            service,
            CreatePartnerRequest(
                "CLEAR-TYPE",
                CashDirection.Receipt,
                partnerId: 1,
                amount: 20m));

        await using var updateContext = database.CreateAdditionalContext();
        var updated = await database.CreateVoucherService(
                companyId: 1,
                context: updateContext)
            .UpdateAsync(
            original.Value.Id,
            ToUpdateRequest(original.Value));
        var movements = await updateContext.BusinessPartnerMovements
            .AsNoTracking()
            .Where(movement => movement.CashVoucherId == original.Value.Id)
            .ToListAsync();
        var cashbox = await database.CreateCashboxService(1)
            .GetByIdAsync(1);

        Assert.True(updated.IsSuccess);
        Assert.Null(updated.Value.CashMovementTypeId);
        Assert.False(updated.Value.IsDraft);
        Assert.Single(movements);
        Assert.Equal(1020m, cashbox.Value.CurrentBalance);
    }

    [Fact]
    public async Task CompletingPaymentCannotMakeCashboxNegative()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await AddVoucherAsync(service,
            CreateRequest(
                "CV-TOO-LARGE",
                CashDirection.Payment,
                movementTypeId: 10,
                amount: 1001m));

        Assert.Equal(
            "CashVouchers.InsufficientCashboxBalance",
            result.Error.Code);
        var stored = Assert.Single(await database.Context.CashVouchers
            .AsNoTracking()
            .ToListAsync());
        Assert.False(stored.IsPosted);
        var cashbox = await database.CreateCashboxService(1)
            .GetByIdAsync(1);
        Assert.Equal(1000m, cashbox.Value.CurrentBalance);
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
                movementTypeId: 9,
                amount: 10m));
        var crossCompanyCashbox = await AddVoucherAsync(service,
            CreateRequest(
                "CV-CROSS-BOX",
                CashDirection.Receipt,
                cashboxId: 4,
                movementTypeId: 9,
                amount: 10m));
        var crossCompanyMovement = await AddVoucherAsync(service,
            CreateRequest(
                "CV-CROSS-MOVEMENT",
                CashDirection.Receipt,
                movementTypeId: 6,
                amount: 10m));
        var crossCompanyPartner = await AddVoucherAsync(service,
            CreatePartnerRequest(
                "CV-CROSS-PARTNER",
                CashDirection.Receipt,
                partnerId: 3,
                amount: 10m));

        Assert.Equal(
            "CashVouchers.MovementTypeDirectionMismatch",
            mismatch.Error.Code);
        Assert.Equal(
            "CashVouchers.CashboxNotFound",
            crossCompanyCashbox.Error.Code);
        Assert.Equal(
            "CashVouchers.MovementTypeNotFound",
            crossCompanyMovement.Error.Code);
        Assert.Equal(
            "CashVouchers.PartnerNotFound",
            crossCompanyPartner.Error.Code);
    }

    [Fact]
    public async Task VoucherAllowsPartyWithOptionalMovementDescriptor()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await AddVoucherAsync(
            service,
            CreatePartnerRequest(
                "CV-PARTY-AND-TYPE",
                CashDirection.Receipt,
                partnerId: 1,
                amount: 10m) with
            {
                CashMovementTypeId = 9
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(CashPartyType.Partner, result.Value.PartyType);
        Assert.Equal(9, result.Value.CashMovementTypeId);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(9)]
    public async Task VoucherAllowsMovementDescriptorWithDirectAccount(
        int movementTypeId)
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await AddVoucherAsync(
            service,
            CreateRequest(
                "CV-INVALID-MANUAL-TYPE",
                CashDirection.Receipt,
                movementTypeId: movementTypeId,
                amount: 10m));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.AccountId);
        Assert.Equal(movementTypeId, result.Value.CashMovementTypeId);
    }

    [Fact]
    public async Task VoucherAllowsDescriptorClassificationIndependentOfDirection()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO CashMovementTypes (
                Id, CompanyId, Name, Direction, Classification, PartnerEffect,
                IsActive, IsDefaultForSales, IsDefaultForPurchase,
                IsDefaultForSalesReturn, IsDefaultForPurchaseReturn,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (101, 1, 'Receipt Expense', 1, 2, 0,
                 1, 0, 0, 0, 0, 'test', '2026-01-01', 'test', 0),
                (102, 1, 'Payment Revenue', 2, 3, 0,
                 1, 0, 0, 0, 0, 'test', '2026-01-01', 'test', 0);
            """);
        database.Context.ChangeTracker.Clear();
        var service = database.CreateVoucherService(companyId: 1);

        var receiptExpense = await AddVoucherAsync(
            service,
            CreateRequest(
                "CV-RECEIPT-EXPENSE",
                CashDirection.Receipt,
                movementTypeId: 101,
                amount: 10m));
        var paymentRevenue = await AddVoucherAsync(
            service,
            CreateRequest(
                "CV-PAYMENT-REVENUE",
                CashDirection.Payment,
                movementTypeId: 102,
                amount: 10m));

        Assert.True(receiptExpense.IsSuccess);
        Assert.Equal(1, receiptExpense.Value.AccountId);
        Assert.Equal(101, receiptExpense.Value.CashMovementTypeId);
        Assert.True(paymentRevenue.IsSuccess);
        Assert.Equal(2, paymentRevenue.Value.AccountId);
        Assert.Equal(102, paymentRevenue.Value.CashMovementTypeId);
    }

    [Fact]
    public async Task UpdateRejectsPreviouslySelectedMovementWhenItIsInactive()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var original = await AddVoucherAsync(
            service,
            CreateRequest(
                "CV-INACTIVE-CURRENT-TYPE",
                CashDirection.Receipt,
                movementTypeId: 9,
                amount: 10m));
        Assert.True(original.IsSuccess);
        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE CashMovementTypes SET IsActive = 0 WHERE Id = 9;");
        database.Context.ChangeTracker.Clear();

        var result = await service.UpdateAsync(
            original.Value.Id,
            ToUpdateRequest(original.Value));

        Assert.Equal("CashVouchers.MovementTypeInactive", result.Error.Code);
    }

    [Theory]
    [InlineData(
        CashDirection.Receipt,
        BusinessPartnerMovementType.CashReceipt,
        0,
        150)]
    [InlineData(
        CashDirection.Payment,
        BusinessPartnerMovementType.CashPayment,
        150,
        0)]
    public async Task PartnerVoucherCreatesExactlyOneConfiguredMovement(
        CashDirection direction,
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
                Description: "Employee cash payment",
                AccountId: 2));

        var result = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: null,
                EmployeeId: 1,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 75m,
                ReferenceNumber: null,
                Description: "Employee cash payment",
                Notes: null,
                RowVersion: draft.Value.RowVersion,
                EmployeeMovementType: EmployeeMovementType.Advance));

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

    [Fact]
    public async Task PostingIntegration_DraftIsHandoverOnlyUntilCompletion()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var createService = database.CreatePostingVoucherService(1);

        var draft = await createService.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 15),
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                Amount: 125m,
                 Description: "Posting integration",
                 Notes: "Counted during handover"));

        Assert.True(draft.IsSuccess, draft.Error.Description);
        Assert.True(draft.Value.IsDraft);
        Assert.False(await database.Context.JournalEntries
            .AnyAsync(entry =>
                entry.SourceType == JournalEntrySourceType.CashVoucher &&
                entry.SourceId == draft.Value.Id));
        var cashboxBefore = await database.CreateCashboxService(1)
            .GetByIdAsync(1);
        Assert.Equal(1000m, cashboxBefore.Value.CurrentBalance);

        var handover = await createService.GetHandoverReportAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 50 },
            new CashVoucherHandoverReportFilterRequest(
                CashboxId: 1,
                FromDate: draft.Value.VoucherDate,
                ToDate: draft.Value.VoucherDate));
        Assert.True(handover.IsSuccess, handover.Error.Description);
        Assert.Contains(handover.Value.Items, item =>
            item.Id == draft.Value.Id &&
            item.Amount == 125m &&
            item.Notes == "Counted during handover");
        Assert.Contains(handover.Value.Summaries, summary =>
            summary.Currency == CurrencyCode.EGP &&
            summary.Receipt == 125m &&
            summary.Payment == 0m &&
            summary.Net == 125m);

        var draftStatement = await database.CreateStatementService(1)
            .GetCashboxStatementAsync(
                new PaginationRequest { PageNumber = 1, PageSize = 50 },
                new CashboxStatementFilterRequest(
                    CashboxId: 1,
                    FromDate: draft.Value.VoucherDate,
                    ToDate: draft.Value.VoucherDate));
        Assert.True(draftStatement.IsSuccess, draftStatement.Error.Description);
        Assert.DoesNotContain(draftStatement.Value.Items, item =>
            item.CashVoucherId == draft.Value.Id);

        await using var updateContext = database.CreateAdditionalContext();
        var posted = await database.CreatePostingVoucherService(
            companyId: 1,
            context: updateContext)
            .UpdateAsync(
                draft.Value.Id,
                new CashVoucherUpdateRequest(
                    VoucherDate: draft.Value.VoucherDate,
                    Direction: draft.Value.Direction,
                    CashboxId: 1,
                    CashMovementTypeId: null,
                    EmployeeId: null,
                    BusinessPartnerId: null,
                    DriverId: null,
                    DriverTripId: null,
                    ExternalPartyName: null,
                    Amount: draft.Value.Amount,
                    ReferenceNumber: null,
                    Description: draft.Value.Description,
                    Notes: null,
                    RowVersion: draft.Value.RowVersion,
                    AccountId: 1));

        Assert.True(posted.IsSuccess, posted.Error.Description);
        Assert.False(posted.Value.IsDraft);

        var entry = await database.Context.JournalEntries
            .AsNoTracking()
            .SingleAsync(item =>
                item.SourceType == JournalEntrySourceType.CashVoucher &&
                item.SourceId == draft.Value.Id);
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);

        var lines = await database.Context.JournalEntryLines
            .AsNoTracking()
            .Where(line => line.JournalEntryId == entry.Id)
            .OrderBy(line => line.Id)
            .ToListAsync();
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, line =>
            line.AccountId == 100 &&
            line.PartyType == JournalPartyType.Cashbox &&
            line.PartyId == 1 &&
            line.Debit == 125m &&
            line.Credit == 0m);
        Assert.Contains(lines, line =>
            line.AccountId == 1 &&
            line.PartyType is null &&
            line.PartyId is null &&
            line.Debit == 0m &&
            line.Credit == 125m);

        var statement = await database.CreateStatementService(1)
            .GetCashboxStatementAsync(
                new PaginationRequest { PageNumber = 1, PageSize = 50 },
                new CashboxStatementFilterRequest(
                    CashboxId: 1,
                    FromDate: draft.Value.VoucherDate,
                    ToDate: draft.Value.VoucherDate));
        Assert.True(statement.IsSuccess, statement.Error.Description);
        Assert.Contains(statement.Value.Items, item =>
            item.CashVoucherId == draft.Value.Id &&
            item.ReceiptAmount == 125m &&
            item.JournalEntryId == entry.Id);

        var completedHandover = await database.CreatePostingVoucherService(1)
            .GetHandoverReportAsync(
                new PaginationRequest { PageNumber = 1, PageSize = 50 },
                new CashVoucherHandoverReportFilterRequest(CashboxId: 1));
        Assert.True(completedHandover.IsSuccess, completedHandover.Error.Description);
        Assert.DoesNotContain(completedHandover.Value.Items, item =>
            item.Id == draft.Value.Id);

        await using var deleteContext = database.CreateAdditionalContext();
        var deleted = await database.CreatePostingVoucherService(
                companyId: 1,
                context: deleteContext)
            .DeleteAsync(draft.Value.Id);

        Assert.True(deleted.IsSuccess, deleted.Error.Description);
        Assert.False(await deleteContext.CashVouchers
            .AnyAsync(item => item.Id == draft.Value.Id));
        Assert.False(await deleteContext.JournalEntries
            .AnyAsync(item =>
                item.SourceType == JournalEntrySourceType.CashVoucher &&
                item.SourceId == draft.Value.Id));
    }

    [Fact]
    public async Task HandoverReportFiltersByCompanyDirectionDateSearchAndCurrency()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var companyOne = database.CreateVoucherService(1);
        var companyTwo = database.CreateVoucherService(2);

        var companyOneReceipt = await companyOne.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 1),
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                Amount: 100m,
                Description: "North handover",
                Notes: "alpha"));
        var companyOnePayment = await companyOne.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 2),
                Direction: CashDirection.Payment,
                CashboxId: 1,
                Amount: 40m,
                Description: "South handover",
                Notes: "beta"));
        var companyOneUsd = await companyOne.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 3),
                Direction: CashDirection.Receipt,
                CashboxId: 5,
                Amount: 10m,
                Description: "USD handover",
                Notes: "currency"));
        var companyTwoReceipt = await companyTwo.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 1),
                Direction: CashDirection.Receipt,
                CashboxId: 4,
                Amount: 99m,
                Description: "Other company handover",
                Notes: "isolated"));

        Assert.True(companyOneReceipt.IsSuccess, companyOneReceipt.Error.Description);
        Assert.True(companyOnePayment.IsSuccess, companyOnePayment.Error.Description);
        Assert.True(companyOneUsd.IsSuccess, companyOneUsd.Error.Description);
        Assert.True(companyTwoReceipt.IsSuccess, companyTwoReceipt.Error.Description);

        var all = await companyOne.GetHandoverReportAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 50 });

        Assert.True(all.IsSuccess, all.Error.Description);
        Assert.Equal(3, all.Value.TotalCount);
        Assert.DoesNotContain(all.Value.Items, item =>
            item.Description == "Other company handover");
        Assert.Equal(2, all.Value.Summaries.Count);
        Assert.Contains(all.Value.Summaries, summary =>
            summary.Currency == CurrencyCode.EGP &&
            summary.Receipt == 100m &&
            summary.Payment == 40m &&
            summary.Net == 60m &&
            summary.Count == 2);
        Assert.Contains(all.Value.Summaries, summary =>
            summary.Currency == CurrencyCode.USD &&
            summary.Receipt == 10m &&
            summary.Payment == 0m &&
            summary.Net == 10m &&
            summary.Count == 1);

        var paymentOnly = await companyOne.GetHandoverReportAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 50 },
            new CashVoucherHandoverReportFilterRequest(
                Direction: CashDirection.Payment));
        Assert.True(paymentOnly.IsSuccess, paymentOnly.Error.Description);
        Assert.Single(paymentOnly.Value.Items);
        Assert.Equal("South handover", paymentOnly.Value.Items[0].Description);

        var dateOnly = await companyOne.GetHandoverReportAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 50 },
            new CashVoucherHandoverReportFilterRequest(
                FromDate: new DateOnly(2026, 8, 3),
                ToDate: new DateOnly(2026, 8, 3)));
        Assert.True(dateOnly.IsSuccess, dateOnly.Error.Description);
        Assert.Single(dateOnly.Value.Items);
        Assert.Equal(CurrencyCode.USD, dateOnly.Value.Items[0].Currency);

        var searched = await companyOne.GetHandoverReportAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 50 },
            new CashVoucherHandoverReportFilterRequest(Search: "alpha"));
        Assert.True(searched.IsSuccess, searched.Error.Description);
        Assert.Single(searched.Value.Items);
        Assert.Equal("North handover", searched.Value.Items[0].Description);

        var isolated = await companyOne.GetHandoverReportAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 50 },
            new CashVoucherHandoverReportFilterRequest(
                Search: "Other company handover"));
        Assert.True(isolated.IsSuccess, isolated.Error.Description);
        Assert.Empty(isolated.Value.Items);
    }

    [Fact]
    public async Task DirectEmployeePaymentCreatesOneLinkedMovement()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 27),
                Direction: CashDirection.Payment,
                CashboxId: 1,
                Amount: 75m,
                Description: "Employee advance",
                EmployeeId: 1,
                EmployeeMovementType: EmployeeMovementType.Advance));

        var posted = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: null,
                EmployeeId: 1,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 75m,
                ReferenceNumber: null,
                Description: "Employee advance",
                Notes: "Advance from cash",
                RowVersion: draft.Value.RowVersion,
                EmployeeMovementType: EmployeeMovementType.Advance));

        var movements = await database.Context.EmployeeMovements
            .AsNoTracking()
            .Where(item => item.CashVoucherId == draft.Value.Id)
            .ToListAsync();

        Assert.True(posted.IsSuccess);
        var movement = Assert.Single(movements);
        Assert.Equal(1, movement.EmployeeId);
        Assert.Equal(EmployeeMovementType.Advance, movement.Type);
        Assert.Equal(75m, movement.Debit);
        Assert.Equal(0m, movement.Credit);
        Assert.Equal(75m, movement.BaseDebit);
        Assert.Equal("Advance from cash", movement.Notes);
    }

    [Fact]
    public async Task DirectEmployeePaymentInfersMovementTypeWhenOmitted()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 27),
                Direction: CashDirection.Payment,
                CashboxId: 1,
                Amount: 75m,
                Description: "Employee advance",
                EmployeeId: 1,
                EmployeeMovementType: EmployeeMovementType.Advance));

        var posted = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: null,
                EmployeeId: 1,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 75m,
                ReferenceNumber: null,
                Description: "Employee advance",
                Notes: null,
                RowVersion: draft.Value.RowVersion,
                EmployeeMovementType: null));

        Assert.True(
            posted.IsSuccess,
            string.Join(
                "; ",
                posted.Errors.Select(error =>
                    error.Code + ":" + error.Description)));
        var movement = await database.Context.EmployeeMovements
            .AsNoTracking()
            .SingleAsync(item => item.CashVoucherId == draft.Value.Id);
        Assert.Equal(EmployeeMovementType.Advance, movement.Type);
    }

    [Fact]
    public async Task DirectEmployeeUpdateUpdatesLinkedMovementWithoutDuplication()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 27),
                Direction: CashDirection.Payment,
                CashboxId: 1,
                Amount: 75m,
                Description: "Employee cash payment",
                AccountId: 2));

        var posted = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: null,
                EmployeeId: 1,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 75m,
                ReferenceNumber: null,
                Description: "Employee cash payment",
                Notes: null,
                RowVersion: draft.Value.RowVersion,
                EmployeeMovementType: EmployeeMovementType.Advance));

        var movementId = await database.Context.EmployeeMovements
            .Where(item => item.CashVoucherId == posted.Value.Id)
            .Select(item => item.Id)
            .SingleAsync();

        await using var updateContext = database.CreateAdditionalContext();
        var updateService = database.CreateVoucherService(1, updateContext);
        var current = await updateService.GetByIdAsync(posted.Value.Id);
        var updated = await updateService.UpdateAsync(
            posted.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: current.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: null,
                EmployeeId: 1,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 100m,
                ReferenceNumber: null,
                Description: "Employee deduction",
                Notes: "Updated employee movement",
                RowVersion: current.Value.RowVersion,
                EmployeeMovementType: EmployeeMovementType.Deduction));

        var movements = await updateContext.EmployeeMovements
            .AsNoTracking()
            .Where(item => item.CashVoucherId == posted.Value.Id)
            .ToListAsync();

        Assert.True(updated.IsSuccess, string.Join("; ", updated.Errors.Select(error => error.Code + ":" + error.Description)));
        var movement = Assert.Single(movements);
        Assert.Equal(movementId, movement.Id);
        Assert.Equal(EmployeeMovementType.Deduction, movement.Type);
        Assert.Equal(100m, movement.Debit);
        Assert.Equal("Updated employee movement", movement.Notes);
    }

    [Fact]
    public async Task ChangingEmployeeVoucherToAnotherTargetRemovesMovement()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 27),
                Direction: CashDirection.Payment,
                CashboxId: 1,
                Amount: 75m,
                Description: "Employee cash payment",
                AccountId: 2));
        var posted = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: null,
                EmployeeId: 1,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 75m,
                ReferenceNumber: null,
                Description: "Employee cash payment",
                Notes: null,
                RowVersion: draft.Value.RowVersion,
                EmployeeMovementType: EmployeeMovementType.Advance));

        await using var changeContext = database.CreateAdditionalContext();
        var changeService = database.CreateVoucherService(1, changeContext);
        var current = await changeService.GetByIdAsync(posted.Value.Id);
        var changed = await changeService.UpdateAsync(
            posted.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: current.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: 10,
                EmployeeId: null,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 75m,
                ReferenceNumber: null,
                Description: "Operating expense",
                Notes: null,
                RowVersion: current.Value.RowVersion));

        Assert.True(changed.IsSuccess, string.Join("; ", changed.Errors.Select(error => error.Code + ":" + error.Description)));
        Assert.Empty(await changeContext.EmployeeMovements
            .Where(item => item.CashVoucherId == posted.Value.Id)
            .ToListAsync());
        Assert.True(await changeContext.EmployeeMovements
            .IgnoreQueryFilters()
            .AnyAsync(item => item.CashVoucherId == posted.Value.Id && item.IsDeleted));
    }

    [Fact]
    public async Task DeletingEmployeeVoucherRemovesLinkedMovement()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 27),
                Direction: CashDirection.Payment,
                CashboxId: 1,
                Amount: 50m,
                Description: "Employee cash payment",
                EmployeeId: 1,
                EmployeeMovementType: EmployeeMovementType.Advance));
        var posted = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: null,
                EmployeeId: 1,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 50m,
                ReferenceNumber: null,
                Description: "Employee cash payment",
                Notes: null,
                RowVersion: draft.Value.RowVersion,
                EmployeeMovementType: EmployeeMovementType.Withdrawal));

        await using var deleteContext = database.CreateAdditionalContext();
        var deleteService = database.CreateVoucherService(1, deleteContext);
        var current = await deleteService.GetByIdAsync(posted.Value.Id);
        var deleted = await deleteService.DeleteAsync(current.Value.Id);

        Assert.True(deleted.IsSuccess, string.Join("; ", deleted.Errors.Select(error => error.Code + ":" + error.Description)));
        Assert.Empty(await deleteContext.EmployeeMovements
            .Where(item => item.CashVoucherId == posted.Value.Id)
            .ToListAsync());
        Assert.True(await deleteContext.EmployeeMovements
            .IgnoreQueryFilters()
            .AnyAsync(item => item.CashVoucherId == posted.Value.Id && item.IsDeleted));
    }

    [Theory]
    [InlineData(EmployeeMovementType.Credit)]
    [InlineData(EmployeeMovementType.Bonus)]
    public async Task DirectEmployeeReceiptCreatesCreditMovement(
        EmployeeMovementType movementType)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 27),
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                Amount: 40m,
                Description: "Employee receipt",
                EmployeeId: 1,
                EmployeeMovementType: movementType));

        var posted = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                CashMovementTypeId: null,
                EmployeeId: 1,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 40m,
                ReferenceNumber: null,
                Description: "Employee receipt",
                Notes: null,
                RowVersion: draft.Value.RowVersion,
                EmployeeMovementType: movementType));

        var movement = await database.Context.EmployeeMovements
            .AsNoTracking()
            .SingleAsync(item => item.CashVoucherId == posted.Value.Id);

        Assert.True(posted.IsSuccess);
        Assert.Equal(movementType, movement.Type);
        Assert.Equal(0m, movement.Debit);
        Assert.Equal(40m, movement.Credit);
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
                Description: "Employee validation",
                AccountId: 2));

        var result = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: null,
                EmployeeId: employeeId,
                BusinessPartnerId: null,
                DriverId: null,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 25m,
                ReferenceNumber: null,
                Description: "Employee validation",
                Notes: null,
                RowVersion: draft.Value.RowVersion,
                EmployeeMovementType: EmployeeMovementType.Advance));

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
                Description: "Historical external party",
                AccountId: 1));

        var result = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                CashMovementTypeId: null,
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
                Description: "Invalid party selection",
                AccountId: 2));

        var result = await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: draft.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: 1,
                CashMovementTypeId: null,
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
    public async Task EmployeeVoucherPersistsDisplayAndSupportsFiltersAndSearch()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await AddVoucherAsync(
            service,
            CreateEmployeeRequest(employeeId: 1, amount: 75m));
        var byEmployee = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new CashVoucherFilterRequest(EmployeeId: 1));
        var bySearch = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new CashVoucherFilterRequest(Search: "EMP-1"));

        Assert.True(result.IsSuccess);
        Assert.Equal(CashPartyType.Employee, result.Value.PartyType);
        Assert.Equal(1, result.Value.EmployeeId);
        Assert.Equal("Employee One", result.Value.EmployeeName);
        Assert.Equal(result.Value.Id, Assert.Single(byEmployee.Value.Items).Id);
        Assert.Equal(result.Value.Id, Assert.Single(bySearch.Value.Items).Id);
        Assert.Null(result.Value.BusinessPartnerId);
        Assert.Null(result.Value.DriverId);
        Assert.Null(result.Value.ExternalPartyName);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(999)]
    public async Task EmployeeVoucherRejectsInactiveCrossCompanyAndMissingEmployee(
        int employeeId)
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await AddVoucherAsync(
            service,
            CreateEmployeeRequest(employeeId, amount: 10m));

        Assert.Equal("CashVouchers.EmployeeNotFound", result.Error.Code);
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
                movementTypeId: 10,
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
                partnerId: 1,
                amount: 100m));

        await using var updateContext = database.CreateAdditionalContext();
        var updateService = database.CreateVoucherService(
            companyId: 1,
            updateContext);
        var updated = await updateService.UpdateAsync(
            created.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: created.Value.VoucherDate,
                Direction: CashDirection.Payment,
                CashboxId: created.Value.CashboxId,
                CashMovementTypeId: null,
                EmployeeId: null,
                BusinessPartnerId: null,
                DriverId: 1,
                DriverTripId: null,
                ExternalPartyName: null,
                Amount: 100m,
                ReferenceNumber: null,
                Description: "Driver advance",
                Notes: null,
                RowVersion: created.Value.RowVersion));

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
                BusinessPartnerId, Amount, Currency, IsPosted, LastModifiedAt,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (
                100, 1, 1, 'INV-PAY-1', '2026-07-25',
                1, 1, 1, 2,
                1, 25, 1, 1, '2026-07-25',
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
                movementTypeId: 9,
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

    [Fact]
    public async Task BulkAddWithoutMovementTypePostsPartnerVoucherImmediately()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await service.BulkAsync(
            new CashVoucherBulkRequest(
            [
                new CashVoucherBulkAddItemRequest(
                    Voucher: CreateBulkVoucher(
                        direction: CashDirection.Receipt,
                        movementTypeId: null,
                        partnerId: 1,
                        amount: 125m))
            ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Summary.Added);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("Added", item.Status);
        Assert.NotNull(item.Voucher);
        Assert.False(item.Voucher!.IsDraft);
        Assert.Null(item.Voucher.CashMovementTypeId);
        Assert.Equal(CashPartyType.Partner, item.Voucher.PartyType);
        Assert.True((await database.Context.CashVouchers
            .AsNoTracking()
            .SingleAsync(voucher => voucher.Id == item.Id)).IsPosted);
        Assert.Single(
            await database.Context.BusinessPartnerMovements
                .Where(movement => movement.CashVoucherId == item.Id)
                .ToListAsync());
    }

    [Fact]
    public async Task BulkUpdateCanClearMovementTypeAndRemainPosted()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var original = await AddVoucherAsync(
            service,
            CreatePartnerRequest(
                "BULK-CLEAR-TYPE",
                CashDirection.Receipt,
                partnerId: 1,
                amount: 10m) with
            {
                CashMovementTypeId = 9
            });

        var result = await service.BulkAsync(
            new CashVoucherBulkRequest(
            [
                new CashVoucherBulkUpdateItemRequest(
                    Id: original.Value.Id,
                    RowVersion: original.Value.RowVersion,
                    Voucher: CreateBulkVoucher(
                        direction: CashDirection.Receipt,
                        movementTypeId: null,
                        partnerId: 1,
                        amount: 30m))
            ]));
        var item = Assert.Single(result.Value.Items);
        var cashbox = await database.CreateCashboxService(1)
            .GetByIdAsync(1);

        Assert.True(result.IsSuccess);
        Assert.NotNull(item.Voucher);
        Assert.Null(item.Voucher!.CashMovementTypeId);
        Assert.False(item.Voucher.IsDraft);
        Assert.True((await database.Context.CashVouchers
            .AsNoTracking()
            .SingleAsync(voucher => voucher.Id == item.Id)).IsPosted);
        Assert.Equal(1030m, cashbox.Value.CurrentBalance);
    }

    [Fact]
    public async Task BulkAppliesMixedAddUpdateAndDeleteAndReturnsCounts()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var updated = await AddVoucherAsync(
            service,
            CreateRequest(
                "BULK-UPDATE",
                CashDirection.Receipt,
                movementTypeId: 9,
                amount: 10m));
        var deleted = await AddVoucherAsync(
            service,
            CreateRequest(
                "BULK-DELETE",
                CashDirection.Receipt,
                movementTypeId: 9,
                amount: 15m));

        var result = await service.BulkAsync(
            new CashVoucherBulkRequest(
            [
                new CashVoucherBulkAddItemRequest(
                    Voucher: CreateBulkVoucher(
                        direction: CashDirection.Payment,
                        movementTypeId: 4,
                        amount: 20m)),
                new CashVoucherBulkUpdateItemRequest(
                    Id: updated.Value.Id,
                    RowVersion: updated.Value.RowVersion,
                    Voucher: CreateBulkVoucher(
                        direction: CashDirection.Receipt,
                        movementTypeId: 3,
                        amount: 30m)),
                new CashVoucherBulkDeleteItemRequest(
                    Id: deleted.Value.Id,
                    RowVersion: deleted.Value.RowVersion)
            ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Summary.Added);
        Assert.Equal(1, result.Value.Summary.Updated);
        Assert.Equal(1, result.Value.Summary.Deleted);
        Assert.Collection(
            result.Value.Items,
            added => Assert.Equal("Added", added.Status),
            changed => Assert.Equal(30m, changed.Voucher!.Amount),
            removed => Assert.Null(removed.Voucher));
        Assert.Empty(
            await database.Context.CashVouchers
                .Where(voucher => voucher.Id == deleted.Value.Id)
                .ToListAsync());
    }

    [Fact]
    public async Task BulkFailureRollsBackEarlierItemsAndReportsItemIndex()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await service.BulkAsync(
            new CashVoucherBulkRequest(
            [
                new CashVoucherBulkAddItemRequest(
                    Voucher: CreateBulkVoucher(
                        direction: CashDirection.Receipt,
                        movementTypeId: 3,
                        amount: 10m)),
                new CashVoucherBulkUpdateItemRequest(
                    Id: 9999,
                    RowVersion: new byte[8],
                    Voucher: CreateBulkVoucher(
                        direction: CashDirection.Receipt,
                        movementTypeId: 3,
                        amount: 10m))
            ]));

        Assert.True(result.IsFailure);
        Assert.Equal("CashVouchers.NotFound", result.Error.Code);
        Assert.Equal("Items[1]", result.Error.FieldName);
        Assert.Empty(await database.Context.CashVouchers.ToListAsync());
    }

    [Fact]
    public async Task BulkRejectsStaleRowVersionAndPreservesVoucher()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);
        var created = await AddVoucherAsync(
            service,
            CreateRequest(
                "BULK-STALE",
                CashDirection.Receipt,
                movementTypeId: 9,
                amount: 10m));
        await using var winnerContext = database.CreateAdditionalContext();
        var winnerService = database.CreateVoucherService(
            companyId: 1,
            context: winnerContext);
        var winner = await winnerService.UpdateAsync(
            created.Value.Id,
            ToUpdateRequest(created.Value, amount: 20m));

        var result = await service.BulkAsync(
            new CashVoucherBulkRequest(
            [
                new CashVoucherBulkUpdateItemRequest(
                    Id: created.Value.Id,
                    RowVersion: created.Value.RowVersion,
                    Voucher: CreateBulkVoucher(
                        direction: CashDirection.Receipt,
                        movementTypeId: 3,
                        amount: 30m))
            ]));

        Assert.True(winner.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("CashVouchers.Concurrency", result.Error.Code);
        Assert.Equal("Items[0]", result.Error.FieldName);
        Assert.Equal(
            20m,
            (await service.GetByIdAsync(created.Value.Id)).Value.Amount);
    }

    [Fact]
    public async Task BulkRejectsOtherCompanyCashbox()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateVoucherService(companyId: 1);

        var result = await service.BulkAsync(
            new CashVoucherBulkRequest(
            [
                new CashVoucherBulkAddItemRequest(
                    Voucher: CreateBulkVoucher(
                        direction: CashDirection.Receipt,
                        cashboxId: 4,
                        movementTypeId: 3,
                        amount: 10m))
            ]));

        Assert.True(result.IsFailure);
        Assert.Equal("CashVouchers.CashboxNotFound", result.Error.Code);
        Assert.Equal("Items[0].Voucher.CashboxId", result.Error.FieldName);
    }

    [Fact]
    public async Task BulkRejectsInvoiceGeneratedVoucherDelete()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO CashVouchers (
                Id, CompanyId, InvoiceId, VoucherNumber, VoucherDate,
                Direction, CashboxId, CashMovementTypeId, PartyType,
                BusinessPartnerId, Amount, Currency, IsPosted, LastModifiedAt,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (
                101, 1, 1, 'INV-BULK-1', '2026-07-25',
                1, 1, 1, 2,
                1, 25, 1, 1, '2026-07-25',
                'test', '2026-07-25', 'test', 0);
            """);
        var service = database.CreateVoucherService(companyId: 1);
        var generated = await service.GetByIdAsync(101);

        var result = await service.BulkAsync(
            new CashVoucherBulkRequest(
            [
                new CashVoucherBulkDeleteItemRequest(
                    Id: 101,
                    RowVersion: generated.Value.RowVersion)
            ]));

        Assert.True(result.IsFailure);
        Assert.Equal("CashVouchers.InvoiceGeneratedReadOnly", result.Error.Code);
        Assert.Equal("Items[0]", result.Error.FieldName);
    }

    private static CashVoucherBulkVoucherRequest CreateBulkVoucher(
        CashDirection direction,
        int cashboxId = 1,
        int? movementTypeId = 3,
        int? partnerId = 1,
        decimal amount = 10m,
        decimal? exchangeRate = null) =>
        new(
            VoucherDate: new DateOnly(2026, 7, 27),
            Direction: direction,
            CashboxId: cashboxId,
            CashMovementTypeId: movementTypeId,
            EmployeeId: null,
            BusinessPartnerId: partnerId,
            DriverId: null,
            DriverTripId: null,
            ExternalPartyName: null,
            Amount: amount,
            ReferenceNumber: "BULK",
            Description: "Bulk voucher",
            Notes: "Bulk notes",
            ExchangeRate: exchangeRate);

    private static VoucherTestRequest CreateRequest(
        string number,
        CashDirection direction,
        int cashboxId = 1,
        int? movementTypeId = null,
        decimal amount = 10m,
        int? accountId = null) =>
        new(
            VoucherDate: new DateOnly(2026, 7, 27),
            Direction: direction,
            CashboxId: cashboxId,
            CashMovementTypeId: movementTypeId ??
                (direction == CashDirection.Receipt ? 9 : 10),
            EmployeeId: null,
            BusinessPartnerId: null,
            DriverId: null,
            DriverTripId: null,
            ExternalPartyName: null,
            Amount: amount,
            ReferenceNumber: null,
            Description: null,
            Notes: null,
            AccountId: accountId ??
                (direction == CashDirection.Receipt ? 1 : 2));

    private static VoucherTestRequest CreatePartnerRequest(
        string number,
        CashDirection direction,
        int partnerId,
        decimal amount,
        int cashboxId = 1,
        decimal? exchangeRate = null) =>
        new(
            VoucherDate: new DateOnly(2026, 7, 27),
            Direction: direction,
            CashboxId: cashboxId,
            CashMovementTypeId: null,
            EmployeeId: null,
            BusinessPartnerId: partnerId,
            DriverId: null,
            DriverTripId: null,
            ExternalPartyName: null,
            Amount: amount,
            ReferenceNumber: null,
            Description: null,
            Notes: null,
            ExchangeRate: exchangeRate);

    private static VoucherTestRequest CreateDriverRequest(
        string number,
        CashDirection direction,
        int driverId,
        int? tripId,
        decimal amount) =>
        new(
            VoucherDate: new DateOnly(2026, 7, 27),
            Direction: direction,
            CashboxId: 1,
            CashMovementTypeId: null,
            EmployeeId: null,
            BusinessPartnerId: null,
            DriverId: driverId,
            DriverTripId: tripId,
            ExternalPartyName: null,
            Amount: amount,
            ReferenceNumber: null,
            Description: null,
            Notes: null);

    private static VoucherTestRequest CreateEmployeeRequest(
        int employeeId,
        decimal amount) =>
        new(
            VoucherDate: new DateOnly(2026, 7, 27),
            Direction: CashDirection.Payment,
            CashboxId: 1,
            CashMovementTypeId: null,
            EmployeeId: employeeId,
            BusinessPartnerId: null,
            DriverId: null,
            DriverTripId: null,
            ExternalPartyName: null,
            Amount: amount,
            ReferenceNumber: null,
            Description: "Employee payment",
            Notes: null);

    private static CashVoucherUpdateRequest ToUpdateRequest(
        CashVoucherResponse original,
        int? cashboxId = null,
        decimal? amount = null) =>
        new(
            VoucherDate: original.VoucherDate,
            Direction: original.Direction,
            CashboxId: cashboxId ?? original.CashboxId,
            CashMovementTypeId: original.CashMovementTypeId,
            EmployeeId: original.EmployeeId,
            BusinessPartnerId: original.BusinessPartnerId,
            DriverId: original.DriverId,
            DriverTripId: original.DriverTripId,
            ExternalPartyName: original.ExternalPartyName,
            Amount: amount ?? original.Amount,
            ReferenceNumber: original.ReferenceNumber,
            Description: original.Description,
            Notes: original.Notes,
            RowVersion: original.RowVersion,
            AccountId: original.AccountId,
            EmployeeMovementType: original.EmployeeId.HasValue
                ? original.Direction == CashDirection.Receipt
                    ? EmployeeMovementType.Credit
                    : EmployeeMovementType.Advance
                : null);

    private static async Task<Result<CashVoucherResponse>> AddVoucherAsync(
        ICashVoucherService service,
        VoucherTestRequest request)
    {
        var draft = await service.AddAsync(
            new CashVoucherRequest(
                VoucherDate: request.VoucherDate,
                Direction: request.Direction,
                CashboxId: request.CashboxId,
                Amount: request.Amount,
                Description: request.Description,
                CashMovementTypeId: request.CashMovementTypeId,
                EmployeeId: request.EmployeeId,
                BusinessPartnerId: request.BusinessPartnerId,
                DriverId: request.DriverId,
                DriverTripId: request.DriverTripId,
                ExternalPartyName: request.ExternalPartyName,
                ReferenceNumber: request.ReferenceNumber,
                Notes: request.Notes,
                ExchangeRate: request.ExchangeRate,
                AccountId: request.AccountId,
                EmployeeMovementType: request.EmployeeId.HasValue
                    ? request.Direction == CashDirection.Receipt
                        ? EmployeeMovementType.Credit
                        : EmployeeMovementType.Advance
                    : null));
        if (draft.IsFailure)
        {
            return draft;
        }

        return await service.UpdateAsync(
            draft.Value.Id,
            new CashVoucherUpdateRequest(
                VoucherDate: request.VoucherDate,
                Direction: request.Direction,
                CashboxId: request.CashboxId,
                CashMovementTypeId: request.CashMovementTypeId,
                EmployeeId: request.EmployeeId,
                BusinessPartnerId: request.BusinessPartnerId,
                DriverId: request.DriverId,
                DriverTripId: request.DriverTripId,
                ExternalPartyName: request.ExternalPartyName,
                Amount: request.Amount,
                ReferenceNumber: request.ReferenceNumber,
                Description: request.Description,
                Notes: request.Notes,
                RowVersion: draft.Value.RowVersion,
                ExchangeRate: request.ExchangeRate,
                AccountId: request.AccountId,
                EmployeeMovementType: request.EmployeeId.HasValue
                    ? request.Direction == CashDirection.Receipt
                        ? EmployeeMovementType.Credit
                        : EmployeeMovementType.Advance
                    : null));
    }

    private sealed record VoucherTestRequest(
        DateOnly VoucherDate,
        CashDirection Direction,
        int CashboxId,
        int? CashMovementTypeId,
        int? EmployeeId,
        int? BusinessPartnerId,
        int? DriverId,
        int? DriverTripId,
        string? ExternalPartyName,
        decimal Amount,
        string? ReferenceNumber,
        string? Description,
        string? Notes,
        decimal? ExchangeRate = null,
        int? AccountId = null);
}
