using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.DriverTrips;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.CashManagement;

public sealed class FinancialStatementServiceTests
{
    static FinancialStatementServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task CashboxStatementCalculatesPeriodOpeningAndClosingBalances()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var vouchers = database.CreateVoucherService(companyId: 1);

        await AddVoucherAsync(database, vouchers,
            CreateGeneralVoucher(
                "CV-BEFORE",
                new DateOnly(2026, 7, 10),
                CashDirection.Receipt,
                9,
                100m));
        await AddVoucherAsync(database, vouchers,
            CreateGeneralVoucher(
                "CV-PERIOD-PAY",
                new DateOnly(2026, 7, 22),
                CashDirection.Payment,
                10,
                40m));
        await AddVoucherAsync(database, vouchers,
            CreateGeneralVoucher(
                "CV-PERIOD-RECEIPT",
                new DateOnly(2026, 7, 23),
                CashDirection.Receipt,
                9,
                25m));
        var draft = await database.CreatePostingVoucherService(companyId: 1)
            .AddAsync(
            new CashVoucherRequest(
                new DateOnly(2026, 7, 24),
                CashDirection.Receipt,
                CashboxId: 1,
                Amount: 500m,
                 Description: "Draft excluded from the statement"));

        Assert.True(draft.IsSuccess, draft.Error.Description);

        var result = await database.CreateStatementService(1)
            .GetCashboxStatementAsync(
                Page(),
                new CashboxStatementFilterRequest(
                    1,
                    FromDate: new DateOnly(2026, 7, 20),
                    ToDate: new DateOnly(2026, 7, 31)));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.CashboxId);
        Assert.Equal("Main Cashbox", result.Value.CashboxName);
        Assert.Equal(CurrencyCode.EGP, result.Value.Currency);
        Assert.Equal(1100m, result.Value.Summary.OpeningBalance);
        Assert.Equal(25m, result.Value.Summary.TotalReceipts);
        Assert.Equal(40m, result.Value.Summary.TotalPayments);
        Assert.Equal(1085m, result.Value.Summary.ClosingBalance);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(
            [1060m, 1085m],
            result.Value.Items.Select(item => item.Balance).ToArray());
        Assert.DoesNotContain(result.Value.Items, item =>
            item.CashVoucherId == draft.Value.Id);
    }

    [Fact]
    public async Task CashboxStatementExcludesUnpostedHandoverDraft()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var vouchers = database.CreateVoucherService(companyId: 1);
        var bulk = await vouchers.BulkAsync(
            new CashVoucherBulkRequest(
            [
                new CashVoucherBulkAddItemRequest(
                    Voucher: new CashVoucherBulkVoucherRequest(
                        VoucherDate: new DateOnly(2026, 8, 3),
                        Direction: CashDirection.Receipt,
                        CashboxId: 1,
                        CashMovementTypeId: null,
                        EmployeeId: null,
                        BusinessPartnerId: null,
                        DriverId: null,
                        DriverTripId: null,
                        ExternalPartyName: null,
                        Amount: 30m,
                        ReferenceNumber: null,
                        Description: "Posted without category",
                        Notes: null,
                        ExchangeRate: null,
                        AccountId: 1))
            ]));
        var draft = await database.CreatePostingVoucherService(companyId: 1)
            .AddAsync(
            new CashVoucherRequest(
                VoucherDate: new DateOnly(2026, 8, 4),
                Direction: CashDirection.Receipt,
                CashboxId: 1,
                Amount: 50m,
                 Description: "Draft handover only"));

        Assert.True(bulk.IsSuccess, bulk.Error.Description);
        Assert.True(draft.IsSuccess, draft.Error.Description);
        await SeedVoucherJournalAsync(database, bulk.Value.Items[0].Id);
        var result = await database.CreateStatementService(1)
            .GetCashboxStatementAsync(
                Page(),
                new CashboxStatementFilterRequest(CashboxId: 1));
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Contains(result.Value.Items, item =>
            item.CashVoucherId == bulk.Value.Items[0].Id &&
            item.MovementName == "سند قبض" &&
            item.ReceiptAmount == 30m);
        Assert.DoesNotContain(result.Value.Items, item =>
            item.CashVoucherId == draft.Value.Id);
        Assert.Equal(30m, result.Value.Summary.TotalReceipts);
        Assert.Equal(1030m, result.Value.Summary.ClosingBalance);
    }

    [Fact]
    public async Task CashboxStatementFiltersByMovementClassification()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var vouchers = database.CreateVoucherService(companyId: 1);
        await AddVoucherAsync(database, vouchers,
            CreateGeneralVoucher(
                "CV-OTHER",
                new DateOnly(2026, 7, 22),
                CashDirection.Receipt,
                9,
                25m));
        var expense = await AddVoucherAsync(database, vouchers,
            CreateGeneralVoucher(
                "CV-EXPENSE",
                new DateOnly(2026, 7, 23),
                CashDirection.Payment,
                10,
                10m));

        var result = await database.CreateStatementService(1)
            .GetCashboxStatementAsync(
                Page(),
                new CashboxStatementFilterRequest(
                    CashboxId: 1,
                    Classification: CashMovementClassification.Expense));

        var item = Assert.Single(result.Value.Items);
        Assert.Equal(expense.Value.Id, item.CashVoucherId);
        Assert.Equal(10m, result.Value.Summary.TotalPayments);
        Assert.Equal(0m, result.Value.Summary.TotalReceipts);
    }

    [Fact]
    public async Task CashboxStatementFiltersAndDisplaysEmployeeParty()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var employeeVoucher = await AddVoucherAsync(
            database,
            database.CreateVoucherService(companyId: 1),
            CreateEmployeeVoucher(employeeId: 1, amount: 30m));

        var result = await database.CreateStatementService(1)
            .GetCashboxStatementAsync(
                Page(),
                new CashboxStatementFilterRequest(
                    CashboxId: 1,
                    Search: "Employee One",
                    PartyType: CashPartyType.Employee,
                    EmployeeId: 1));

        Assert.True(employeeVoucher.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(employeeVoucher.Value.Id, item.CashVoucherId);
        Assert.Equal("Employee One", item.PartyName);
        Assert.Equal(30m, result.Value.Summary.TotalPayments);
    }

    [Fact]
    public async Task ForeignCurrencyCashboxStatementReturnsAllCurrencyDetails()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE Cashboxes
            SET OpeningBalanceDate = '2026-07-01',
                OpeningExchangeRate = 48,
                BaseOpeningBalance = 4800
            WHERE Id = 5;

            UPDATE JournalEntries
            SET EntryDate = '2026-07-01'
            WHERE SourceType = 11 AND SourceId = 5;

            UPDATE JournalEntryLines
            SET Debit = 4800,
                Currency = 2,
                ExchangeRate = 48,
                TransactionDebit = 100
            WHERE JournalEntryId = (
                SELECT Id FROM JournalEntries
                WHERE SourceType = 11 AND SourceId = 5);

            INSERT INTO CashVouchers (
                CompanyId, VoucherNumber, VoucherDate, Direction,
                CashboxId, CashMovementTypeId, PartyType, Amount,
                Currency, ExchangeRate, BaseAmount, IsPosted, LastModifiedAt,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (
                1, 'USD-RECEIPT', '2026-07-10', 1,
                5, 3, 1, 10,
                2, 50, 500, 1, '2026-07-10',
                'test', '2026-07-10', 'test', 0);
            """);
        var usdVoucherId = await database.Context.CashVouchers
            .Where(voucher => voucher.VoucherNumber == "USD-RECEIPT")
            .Select(voucher => voucher.Id)
            .SingleAsync();
        await SeedVoucherJournalAsync(database, usdVoucherId);

        var result = await database.CreateStatementService(1)
            .GetCashboxStatementAsync(
                Page(),
                new CashboxStatementFilterRequest(CashboxId: 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(CurrencyCode.USD, result.Value.Currency);
        Assert.Equal(CurrencyCode.EGP, result.Value.BaseCurrency);
        Assert.False(result.Value.IsBaseCurrency);
        Assert.Equal(
            new DateOnly(2026, 7, 1),
            result.Value.OpeningBalanceDate);
        Assert.Equal(48m, result.Value.OpeningExchangeRate);
        Assert.Equal(4800m, result.Value.Summary.BaseOpeningBalance);

        var item = Assert.Single(result.Value.Items);
        Assert.Equal(CurrencyCode.USD, item.Currency);
        Assert.Equal(CurrencyCode.EGP, item.BaseCurrency);
        Assert.Equal(50m, item.ExchangeRate);
        Assert.False(item.IsBaseCurrency);
        Assert.Equal(10m, item.ReceiptAmount);
        Assert.Equal(500m, item.BaseReceiptAmount);
        Assert.Equal(110m, item.Balance);
        Assert.Equal(5300m, item.BaseBalance);
    }

    [Fact]
    public async Task PartnerStatementCombinesOpeningInvoiceAndVoucherOnce()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var voucher = await AddVoucherAsync(
            database,
            database.CreateVoucherService(1),
            CreatePartnerVoucher(
                "CV-PARTNER-STMT",
                new DateOnly(2026, 7, 15),
                CashDirection.Receipt,
                amount: 50m));

        var result = await database.CreateStatementService(1)
            .GetPartnerStatementAsync(
                Page(),
                new PartnerStatementFilterRequest(1));

        Assert.True(voucher.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BusinessPartnerId);
        Assert.Equal("Customer One", result.Value.BusinessPartnerName);
        Assert.Equal(CurrencyCode.EGP, result.Value.Currency);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Single(
            result.Value.Items,
            item =>
                item.MovementName == "سند قبض" &&
                item.DebitAmount == 0m &&
                item.CreditAmount == 50m);
        Assert.Equal(250m, result.Value.Summary.ClosingBalanceAmount);
        Assert.Equal(
            "عليه",
            result.Value.Summary.ClosingBalanceDescription);
        Assert.All(
            result.Value.Items,
            item => Assert.False(string.IsNullOrWhiteSpace(
                item.MovementName)));
    }

    [Fact]
    public async Task ForeignCurrencyPartnerStatementReturnsOriginalAndEgpValues()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE BusinessPartners SET Currency = 2 WHERE Id = 2;

            INSERT INTO PartnerOpeningBalances (
                Id, CompanyId, BusinessPartnerId, DocumentNumber,
                DocumentDate, Currency, ExchangeRate, BalanceType,
                Amount, BaseAmount, Notes,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (
                2, 1, 2, 'OPEN-USD', '2026-07-01', 2, 50, 1,
                100, 5000, 'USD opening',
                'test', '2026-07-01', 'test', 0);

            INSERT INTO BusinessPartnerMovements (
                Id, CompanyId, BusinessPartnerId, InvoiceId,
                CashVoucherId, MovementType, MovementDate, Currency,
                Debit, Credit, ExchangeRate, BaseDebit, BaseCredit,
                Description, CreatedById, CreatedOn, CreatedByPc,
                IsDeleted)
            VALUES (
                2, 1, 2, 2, NULL, 1, '2026-07-10', 2,
                20, 0, 51, 1020, 0,
                'USD invoice', 'test', '2026-07-10', 'test', 0);
            """);

        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3010,
            entryNumber: "JE-OPEN-USD-PARTNER",
            entryDate: new DateOnly(2026, 7, 1),
            entryType: JournalEntryType.Opening,
            sourceType: JournalEntrySourceType.PartnerOpeningBalance,
            sourceId: 2,
            sourceNumber: "OPEN-USD",
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 1,
                    PartyType: JournalPartyType.Supplier,
                    PartyId: 2,
                    Debit: 5000m,
                    Credit: 0m,
                    Currency: CurrencyCode.USD,
                    ExchangeRate: 50m,
                    TransactionDebit: 100m,
                    TransactionCredit: 0m,
                    Description: "USD opening")
            ]);
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3011,
            entryNumber: "JE-USD-INVOICE",
            entryDate: new DateOnly(2026, 7, 10),
            entryType: JournalEntryType.Automatic,
            sourceType: JournalEntrySourceType.Invoice,
            sourceId: 2,
            sourceNumber: "INV-2",
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 1,
                    PartyType: JournalPartyType.Supplier,
                    PartyId: 2,
                    Debit: 1020m,
                    Credit: 0m,
                    Currency: CurrencyCode.USD,
                    ExchangeRate: 51m,
                    TransactionDebit: 20m,
                    TransactionCredit: 0m,
                    Description: "USD invoice")
            ]);

        var result = await database.CreateStatementService(1)
            .GetPartnerStatementAsync(
                Page(),
                new PartnerStatementFilterRequest(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(CurrencyCode.USD, result.Value.Currency);
        Assert.Equal(CurrencyCode.EGP, result.Value.BaseCurrency);
        Assert.Collection(
            result.Value.Items,
            opening =>
            {
                Assert.Equal(100m, opening.DebitAmount);
                Assert.Equal(50m, opening.ExchangeRate);
                Assert.Equal(5000m, opening.BaseDebitAmount);
                Assert.Equal(100m, opening.BalanceAmount);
                Assert.Equal(5000m, opening.BaseBalanceAmount);
            },
            invoice =>
            {
                Assert.Equal(20m, invoice.DebitAmount);
                Assert.Equal(51m, invoice.ExchangeRate);
                Assert.Equal(1020m, invoice.BaseDebitAmount);
                Assert.Equal(120m, invoice.BalanceAmount);
                Assert.Equal(6020m, invoice.BaseBalanceAmount);
            });
        Assert.Equal(120m, result.Value.Summary.ClosingBalanceAmount);
        Assert.Equal(
            6020m,
            result.Value.Summary.BaseClosingBalanceAmount);
    }

    [Fact]
    public async Task DriverStatementIncludesGeneralLinkedAndTripCostRows()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var vouchers = database.CreateVoucherService(1);
        await AddVoucherAsync(database, vouchers,
            CreateDriverVoucher(
                "CV-DRIVER-GENERAL",
                new DateOnly(2026, 7, 18),
                CashDirection.Payment,
                tripId: null,
                amount: 100m));
        await AddVoucherAsync(database, vouchers,
            CreateDriverVoucher(
                "CV-DRIVER-LINKED",
                new DateOnly(2026, 7, 21),
                CashDirection.Receipt,
                tripId: 1,
                amount: 20m));

        var trips = database.CreateDriverTripService(1);
        var trip = Assert.Single(
            (await trips.GetCostEntryAsync(
                Page(),
                new DriverTripCostFilterRequest(TripNumber: "TR-1")))
            .Value.Items);
        await trips.UpdateCostsAsync(
            new DriverTripBulkCostUpdateRequest(
            [
                new DriverTripCostUpdateItem(
                    1,
                    60m,
                    "Trip cost",
                    trip.RowVersion)
            ]));
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 3020,
            entryNumber: "JE-DRIVER-TRIP-1",
            entryDate: new DateOnly(2026, 7, 20),
            entryType: JournalEntryType.Automatic,
            sourceType: JournalEntrySourceType.DriverTrip,
            sourceId: 1,
            sourceNumber: "INV-1",
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 2,
                    PartyType: JournalPartyType.Driver,
                    PartyId: 1,
                    Debit: 0m,
                    Credit: 60m,
                    Currency: CurrencyCode.EGP,
                    ExchangeRate: 1m,
                    TransactionDebit: 0m,
                    TransactionCredit: 60m,
                    Description: "Trip cost")
            ]);

        var statements = database.CreateStatementService(1);
        var overall = await statements.GetDriverStatementAsync(
            Page(),
            new DriverStatementFilterRequest(1));
        var tripSpecific = await statements.GetDriverStatementAsync(
            Page(),
            new DriverStatementFilterRequest(
                1,
                DriverTripId: 1));

        Assert.True(overall.IsSuccess);
        Assert.Equal(1, overall.Value.DriverId);
        Assert.Equal("Driver One", overall.Value.DriverName);
        Assert.Equal(3, overall.Value.TotalCount);
        Assert.Equal(100m, overall.Value.Summary.TotalPaidToDriver);
        Assert.Equal(20m, overall.Value.Summary.TotalReceivedFromDriver);
        Assert.Equal(60m, overall.Value.Summary.TotalTripCost);
        Assert.Equal(20m, overall.Value.Summary.ClosingBalanceAmount);
        Assert.Equal(
            "مبلغ مطلوب من السائق",
            overall.Value.Summary.ClosingBalanceDescription);

        Assert.Equal(2, tripSpecific.Value.TotalCount);
        Assert.Equal(0m, tripSpecific.Value.Summary.TotalPaidToDriver);
        Assert.Equal(20m, tripSpecific.Value.Summary.TotalReceivedFromDriver);
        Assert.Equal(60m, tripSpecific.Value.Summary.TotalTripCost);
        Assert.Equal(80m, tripSpecific.Value.Summary.ClosingBalanceAmount);
        Assert.Equal(
            "مبلغ مطلوب دفعه للسائق",
            tripSpecific.Value.Summary.ClosingBalanceDescription);

        var generalVoucher = Assert.Single(
            overall.Value.Items,
            item => item.Description == "CV-DRIVER-GENERAL");
        Assert.Null(generalVoucher.BusinessPartnerId);
        Assert.Null(generalVoucher.BusinessPartnerName);
        Assert.Null(generalVoucher.CountryName);
        Assert.All(
            tripSpecific.Value.Items,
            item =>
            {
                Assert.Equal(1, item.BusinessPartnerId);
                Assert.Equal(
                    "Customer One",
                    item.BusinessPartnerName);
                Assert.Equal("Egypt", item.CountryName);
            });
        Assert.Contains(
            overall.Value.Items,
            item =>
                item.SourceName == "رحلة سائق" &&
                item.MovementName == "تكلفة رحلة");
    }

    [Fact]
    public async Task DeletedVoucherIsExcludedFromEveryStatement()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        var vouchers = database.CreateVoucherService(1);
        var created = await AddVoucherAsync(database, vouchers,
            CreateDriverVoucher(
                "CV-DELETED",
                new DateOnly(2026, 7, 25),
                CashDirection.Payment,
                tripId: null,
                amount: 100m));
        database.Context.ChangeTracker.Clear();
        await vouchers.DeleteAsync(created.Value.Id);
        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE JournalEntries SET IsDeleted = 1 WHERE SourceType = {(int)JournalEntrySourceType.CashVoucher} AND SourceId = {created.Value.Id}");

        var statements = database.CreateStatementService(1);
        var cashbox = await statements.GetCashboxStatementAsync(
            Page(),
            new CashboxStatementFilterRequest(
                1,
                Search: "CV-DELETED"));
        var driver = await statements.GetDriverStatementAsync(
            Page(),
            new DriverStatementFilterRequest(
                1,
                Search: "CV-DELETED"));

        Assert.Empty(cashbox.Value.Items);
        Assert.Empty(driver.Value.Items);
        Assert.Equal(1000m, cashbox.Value.Summary.ClosingBalance);
    }

    [Fact]
    public async Task ContainerStoreStatementReturnsDetailedBalancesAcrossPages()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await SeedContainerStatementDataAsync(database);

        var result = await database.CreateStatementService(1)
            .GetContainerStoreStatementAsync(
                new PaginationRequest
                {
                    PageNumber = 2,
                    PageSize = 2
                },
                new ContainerStoreStatementFilterRequest(
                    BusinessPartnerId: 1,
                    FromDate: new DateOnly(2026, 7, 10),
                    ToDate: new DateOnly(2026, 7, 12)));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BusinessPartner.Id);
        Assert.Equal("BP-1", result.Value.BusinessPartner.Code);
        Assert.Equal("Customer One", result.Value.BusinessPartner.Name);
        Assert.Equal("01000000000", result.Value.BusinessPartner.PhoneNumber);
        Assert.Equal("customer@example.com", result.Value.BusinessPartner.Email);
        Assert.Equal("Customer address", result.Value.BusinessPartner.Address);
        Assert.Equal("TAX-CUSTOMER", result.Value.BusinessPartner.TaxNumber);
        Assert.Equal(
            CurrencyCode.EGP,
            result.Value.BusinessPartner.Currency);
        Assert.True(result.Value.BusinessPartner.IsActive);

        Assert.Equal(1, result.Value.ContainerStore.Id);
        Assert.Equal("CSTORE-1", result.Value.ContainerStore.Code);
        Assert.Equal(
            "Customer One Containers",
            result.Value.ContainerStore.Name);
        Assert.Equal("Container yard", result.Value.ContainerStore.Address);
        Assert.True(result.Value.ContainerStore.IsActive);

        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(2, result.Value.PageSize);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Equal(2, result.Value.TotalPages);

        var item = Assert.Single(result.Value.Items);
        Assert.Equal(4, item.MovementId);
        Assert.Equal(new DateOnly(2026, 7, 12), item.MovementDate);
        Assert.Equal(1, item.InvoiceId);
        Assert.Equal("INV-1", item.InvoiceNumber);
        Assert.Equal("PARTNER-INV-1", item.PartnerInvoiceNumber);
        Assert.Equal(InvoiceType.Sales, item.InvoiceType);
        Assert.Equal(1, item.ContainerId);
        Assert.Equal("CONT-1", item.ContainerCode);
        Assert.Equal("Large Crate", item.ContainerName);
        Assert.Equal("Large reusable crate", item.ContainerDescription);
        Assert.True(item.IsContainerActive);
        Assert.True(item.IsCurrentlyAssignedToStore);
        Assert.Equal(0, item.OutgoingUnits);
        Assert.Equal(2, item.IncomingUnits);
        Assert.Equal(-2, item.NetUnits);
        Assert.Equal(6, item.RunningBalanceUnits);
        Assert.Equal("Customer return", item.MovementDescription);
        Assert.Equal(
            new DateTime(2026, 7, 12, 9, 0, 0, DateTimeKind.Utc),
            item.CreatedOn.ToUniversalTime());

        Assert.Equal(5, result.Value.Summary.OpeningUnits);
        Assert.Equal(7, result.Value.Summary.TotalOutgoingUnits);
        Assert.Equal(2, result.Value.Summary.TotalIncomingUnits);
        Assert.Equal(5, result.Value.Summary.NetUnits);
        Assert.Equal(10, result.Value.Summary.ClosingUnits);
        Assert.Equal(4, result.Value.Summary.DistinctContainerCount);
        Assert.Equal(3, result.Value.Summary.MovementCount);

        Assert.Collection(
            result.Value.Containers,
            first =>
            {
                Assert.Equal(1, first.ContainerId);
                Assert.Equal("CONT-1", first.ContainerCode);
                Assert.Equal("Large Crate", first.ContainerName);
                Assert.True(first.IsContainerActive);
                Assert.True(first.IsCurrentlyAssignedToStore);
                Assert.Equal(5, first.OpeningUnits);
                Assert.Equal(3, first.PeriodOutgoingUnits);
                Assert.Equal(2, first.PeriodIncomingUnits);
                Assert.Equal(1, first.PeriodNetUnits);
                Assert.Equal(6, first.ClosingUnits);
            },
            second =>
            {
                Assert.Equal(2, second.ContainerId);
                Assert.Equal("CONT-2", second.ContainerCode);
                Assert.False(second.IsContainerActive);
                Assert.False(second.IsCurrentlyAssignedToStore);
                Assert.Equal(4, second.PeriodOutgoingUnits);
                Assert.Equal(4, second.PeriodNetUnits);
                Assert.Equal(4, second.ClosingUnits);
            },
            third =>
            {
                Assert.Equal(3, third.ContainerId);
                Assert.Equal("CONT-3", third.ContainerCode);
                Assert.True(third.IsCurrentlyAssignedToStore);
                Assert.Equal(0, third.OpeningUnits);
                Assert.Equal(0, third.PeriodOutgoingUnits);
                Assert.Equal(0, third.PeriodIncomingUnits);
                Assert.Equal(0, third.ClosingUnits);
            },
            fourth =>
            {
                Assert.Equal(5, fourth.ContainerId);
                Assert.Equal("CONT-5", fourth.ContainerCode);
                Assert.False(fourth.IsContainerActive);
                Assert.False(fourth.IsCurrentlyAssignedToStore);
                Assert.Equal(0, fourth.OpeningUnits);
                Assert.Equal(0, fourth.PeriodOutgoingUnits);
                Assert.Equal(0, fourth.PeriodIncomingUnits);
                Assert.Equal(0, fourth.ClosingUnits);
            });
    }

    [Fact]
    public async Task ContainerStoreStatementCombinesAllMovementFilters()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await SeedContainerStatementDataAsync(database);

        var result = await database.CreateStatementService(1)
            .GetContainerStoreStatementAsync(
                Page(),
                new ContainerStoreStatementFilterRequest(
                    BusinessPartnerId: 1,
                    Search: "Needle",
                    FromDate: new DateOnly(2026, 7, 10),
                    ToDate: new DateOnly(2026, 7, 31),
                    ContainerId: 1,
                    InvoiceType: InvoiceType.Sales,
                    InvoiceNumber: "PARTNER-INV",
                    Direction: ContainerMovementDirection.Outgoing));

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(2, item.MovementId);
        Assert.Equal(3, item.OutgoingUnits);
        Assert.Equal(0, item.IncomingUnits);
        Assert.Equal(8, item.RunningBalanceUnits);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal(3, result.Value.Summary.TotalOutgoingUnits);
        Assert.Equal(0, result.Value.Summary.TotalIncomingUnits);
        Assert.Equal(5, result.Value.Summary.OpeningUnits);
        Assert.Equal(8, result.Value.Summary.ClosingUnits);
        Assert.Equal(1, result.Value.Summary.DistinctContainerCount);
    }

    [Fact]
    public async Task ContainerStoreStatementSummaryOpeningMatchesFilteredContainers()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await SeedContainerStatementDataAsync(database);
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ContainerMovements (
                Id, CompanyId, BusinessPartnerId, ContainerStoreId,
                ContainerId, InvoiceId, InvoiceNumber, MovementDate,
                OutgoingUnits, IncomingUnits, Description,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (
                8, 1, 1, 1, 2, 2, 'INV-2', '2026-07-02',
                11, 0, 'Excluded container opening',
                'test', '2026-07-02T09:00:00Z', 'test', 0);
            """);
        database.Context.ChangeTracker.Clear();

        var result = await database.CreateStatementService(1)
            .GetContainerStoreStatementAsync(
                Page(),
                new ContainerStoreStatementFilterRequest(
                    BusinessPartnerId: 1,
                    Search: "Needle",
                    FromDate: new DateOnly(2026, 7, 10),
                    ToDate: new DateOnly(2026, 7, 31)));

        Assert.True(result.IsSuccess);
        var container = Assert.Single(result.Value.Containers);
        Assert.Equal(1, container.ContainerId);
        Assert.Equal(5, container.OpeningUnits);
        Assert.Equal(10, container.PeriodOutgoingUnits);
        Assert.Equal(15, container.ClosingUnits);
        Assert.Equal(5, result.Value.Summary.OpeningUnits);
        Assert.Equal(10, result.Value.Summary.TotalOutgoingUnits);
        Assert.Equal(0, result.Value.Summary.TotalIncomingUnits);
        Assert.Equal(15, result.Value.Summary.ClosingUnits);
        Assert.Equal(1, result.Value.Summary.DistinctContainerCount);
    }

    [Fact]
    public async Task ContainerStoreStatementDoesNotLeakOtherCompanyOrMissingStore()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();

        var otherCompanyPartner = await database.CreateStatementService(1)
            .GetContainerStoreStatementAsync(
                Page(),
                new ContainerStoreStatementFilterRequest(
                    BusinessPartnerId: 3));
        var partnerWithoutStore = await database.CreateStatementService(1)
            .GetContainerStoreStatementAsync(
                Page(),
                new ContainerStoreStatementFilterRequest(
                    BusinessPartnerId: 2));

        Assert.True(otherCompanyPartner.IsFailure);
        Assert.Equal(
            "Statements.ContainerStorePartnerNotFound",
            otherCompanyPartner.Error.Code);
        Assert.True(partnerWithoutStore.IsFailure);
        Assert.Equal(
            "Statements.ContainerStoreNotFound",
            partnerWithoutStore.Error.Code);
    }

    [Fact]
    public void ContainerStoreStatementFilterValidatorRejectsInvalidValues()
    {
        var validator = new ContainerStoreStatementFilterRequestValidator();
        var result = validator.Validate(
            new ContainerStoreStatementFilterRequest(
                BusinessPartnerId: 0,
                Search: new string('x', 257),
                FromDate: new DateOnly(2026, 8, 2),
                ToDate: new DateOnly(2026, 8, 1),
                ContainerId: 0,
                InvoiceType: (InvoiceType)99,
                InvoiceNumber: new string('y', 101),
                Direction: (ContainerMovementDirection)99));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "BusinessPartnerId");
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "Search");
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "ToDate");
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "ContainerId");
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "InvoiceType");
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "InvoiceNumber");
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "Direction");
        Assert.Null(
            typeof(ContainerStoreStatementFilterRequest)
                .GetProperty("ContainerStoreId"));
    }

    private static async Task SeedContainerStatementDataAsync(
        CashManagementTestDatabase database)
    {
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE BusinessPartners
            SET PhoneNumber = '01000000000',
                Email = 'customer@example.com',
                Address = 'Customer address',
                TaxNumber = 'TAX-CUSTOMER'
            WHERE Id = 1;

            UPDATE Invoices
            SET PartnerInvoiceNo = 'PARTNER-INV-1'
            WHERE Id = 1;

            UPDATE Invoices
            SET PartnerInvoiceNo = 'PARTNER-INV-2'
            WHERE Id = 2;

            INSERT INTO ContainerMovements (
                Id, CompanyId, BusinessPartnerId, ContainerStoreId,
                ContainerId, InvoiceId, InvoiceNumber, MovementDate,
                OutgoingUnits, IncomingUnits, Description,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 1, 1, 1, 1, 'INV-1', '2026-07-01',
                 5, 0, 'Opening dispatch',
                 'test', '2026-07-01T09:00:00Z', 'test', 0),
                (2, 1, 1, 1, 1, 1, 'INV-1', '2026-07-10',
                 3, 0, 'Needle outgoing',
                 'test', '2026-07-10T09:00:00Z', 'test', 0),
                (3, 1, 1, 1, 2, 2, 'INV-2', '2026-07-11',
                 4, 0, 'Historical inactive container',
                 'test', '2026-07-11T09:00:00Z', 'test', 0),
                (4, 1, 1, 1, 1, 1, 'INV-1', '2026-07-12',
                 0, 2, 'Customer return',
                 'test', '2026-07-12T09:00:00Z', 'test', 0),
                (5, 1, 1, 1, 1, 2, 'INV-2', '2026-07-13',
                 7, 0, 'Needle purchase movement',
                 'test', '2026-07-13T09:00:00Z', 'test', 0),
                (6, 1, 1, 1, 1, 1, 'INV-1', '2026-07-14',
                 9, 0, 'Needle deleted movement',
                 'test', '2026-07-14T09:00:00Z', 'test', 1),
                (7, 2, 3, 2, 4, 3, 'INV-3', '2026-07-10',
                 99, 0, 'Needle other company',
                 'test', '2026-07-10T09:00:00Z', 'test', 0);
            """);
        database.Context.ChangeTracker.Clear();
    }

    private static PaginationRequest Page() =>
        new()
        {
            PageNumber = 1,
            PageSize = 20
        };

    private static VoucherTestRequest CreateGeneralVoucher(
        string number,
        DateOnly date,
        CashDirection direction,
        int movementTypeId,
        decimal amount) =>
        new(
            VoucherDate: date,
            Direction: direction,
            CashboxId: 1,
            CashMovementTypeId: movementTypeId,
            EmployeeId: null,
            BusinessPartnerId: null,
            DriverId: null,
            DriverTripId: null,
            ExternalPartyName: null,
            Amount: amount,
            ReferenceNumber: null,
            Description: number,
            Notes: null);

    private static VoucherTestRequest CreatePartnerVoucher(
        string number,
        DateOnly date,
        CashDirection direction,
        decimal amount) =>
        new(
            VoucherDate: date,
            Direction: direction,
            CashboxId: 1,
            CashMovementTypeId: null,
            EmployeeId: null,
            BusinessPartnerId: 1,
            DriverId: null,
            DriverTripId: null,
            ExternalPartyName: null,
            Amount: amount,
            ReferenceNumber: "REF-PARTNER",
            Description: number,
            Notes: null);

    private static VoucherTestRequest CreateDriverVoucher(
        string number,
        DateOnly date,
        CashDirection direction,
        int? tripId,
        decimal amount) =>
        new(
            VoucherDate: date,
            Direction: direction,
            CashboxId: 1,
            CashMovementTypeId: null,
            EmployeeId: null,
            BusinessPartnerId: null,
            DriverId: 1,
            DriverTripId: tripId,
            ExternalPartyName: null,
            Amount: amount,
            ReferenceNumber: "REF-DRIVER",
            Description: number,
            Notes: null);

    private static VoucherTestRequest CreateEmployeeVoucher(
        int employeeId,
        decimal amount) =>
        new(
            VoucherDate: new DateOnly(2026, 7, 24),
            Direction: CashDirection.Payment,
            CashboxId: 1,
            CashMovementTypeId: null,
            EmployeeId: employeeId,
            BusinessPartnerId: null,
            DriverId: null,
            DriverTripId: null,
            ExternalPartyName: null,
            Amount: amount,
            ReferenceNumber: "REF-EMPLOYEE",
            Description: "Employee payment",
            Notes: null);

    private static async Task<Result<CashVoucherResponse>> AddVoucherAsync(
        CashManagementTestDatabase database,
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
                EmployeeMovementType: request.EmployeeId.HasValue
                    ? request.Direction == CashDirection.Receipt
                        ? EmployeeMovementType.Credit
                        : EmployeeMovementType.Advance
                    : null));
        if (draft.IsFailure)
        {
            return draft;
        }

        var updated = await service.UpdateAsync(
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
                RowVersion: draft.Value.RowVersion));
        if (updated.IsSuccess && !updated.Value.IsDraft)
        {
            await SeedVoucherJournalAsync(database, updated.Value.Id);
        }
        return updated;
    }

    private static async Task SeedVoucherJournalAsync(
        CashManagementTestDatabase database,
        int voucherId)
    {
        var voucher = await database.Context.CashVouchers
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == voucherId);
        var isReceipt = voucher.Direction == CashDirection.Receipt;
        var journalId = 20_000 + voucher.Id;
        var lines = new List<CashManagementTestDatabase.JournalEntryLineSeed>();

        if (voucher.CashboxId.HasValue)
        {
            lines.Add(new CashManagementTestDatabase.JournalEntryLineSeed(
                AccountId: voucher.AccountId ?? 1,
                PartyType: JournalPartyType.Cashbox,
                PartyId: voucher.CashboxId,
                Debit: isReceipt ? voucher.BaseAmount : 0m,
                Credit: isReceipt ? 0m : voucher.BaseAmount,
                Currency: voucher.Currency,
                ExchangeRate: voucher.ExchangeRate,
                TransactionDebit: isReceipt ? voucher.Amount : 0m,
                TransactionCredit: isReceipt ? 0m : voucher.Amount,
                Description: voucher.Description));
        }

        var (partyType, partyId) = voucher.PartyType switch
        {
            CashPartyType.Partner =>
                (voucher.BusinessPartnerId == 2
                    ? JournalPartyType.Supplier
                    : JournalPartyType.Customer, voucher.BusinessPartnerId),
            CashPartyType.Driver => (JournalPartyType.Driver, voucher.DriverId),
            CashPartyType.Employee =>
                (JournalPartyType.Employee, voucher.EmployeeId),
            _ => ((JournalPartyType?)null, (int?)null)
        };
        var accountId = voucher.AccountId ??
            (voucher.CashMovementTypeId == 9 ? 1 : 2);
        lines.Add(new CashManagementTestDatabase.JournalEntryLineSeed(
            AccountId: accountId,
            PartyType: partyType,
            PartyId: partyId,
            Debit: isReceipt ? 0m : voucher.BaseAmount,
            Credit: isReceipt ? voucher.BaseAmount : 0m,
            Currency: voucher.Currency,
            ExchangeRate: voucher.ExchangeRate,
            TransactionDebit: isReceipt ? 0m : voucher.Amount,
            TransactionCredit: isReceipt ? voucher.Amount : 0m,
            Description: voucher.Description));

        await database.SeedPostedJournalEntryAsync(
            journalEntryId: journalId,
            entryNumber: $"JE-CV-{voucher.Id}",
            entryDate: voucher.VoucherDate,
            entryType: JournalEntryType.Automatic,
            sourceType: JournalEntrySourceType.CashVoucher,
            sourceId: voucher.Id,
            sourceNumber: voucher.VoucherNumber,
            lines: lines);
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
        string? Notes);
}
