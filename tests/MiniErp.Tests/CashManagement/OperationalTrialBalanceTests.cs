using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.CashManagement;

public sealed class OperationalTrialBalanceTests
{
    private static readonly DateOnly FromDate = new(2026, 6, 1);
    private static readonly DateOnly ToDate = new(2026, 6, 30);

    [Fact]
    public async Task DetailedReportCalculatesEverySourceInBaseCurrency()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await SeedReportDataAsync(database);

        var result = await database.CreateStatementService(1)
            .GetOperationalTrialBalanceAsync(
                new OperationalTrialBalanceFilterRequest(
                    FromDate: FromDate,
                    ToDate: ToDate));

        Assert.True(result.IsSuccess);
        Assert.Equal(CurrencyCode.USD, result.Value.BaseCurrency);
        Assert.Equal(
            OperationalTrialBalanceViewMode.Detailed,
            result.Value.ViewMode);
        Assert.Equal(6, result.Value.Items.Count);

        AssertAmounts(
            Find(result.Value, OperationalTrialBalanceCategory.Cashbox),
            openingDebit: 35m,
            openingCredit: 0m,
            periodDebit: 66m,
            periodCredit: 95m,
            closingDebit: 6m,
            closingCredit: 0m);
        AssertAmounts(
            Find(result.Value, OperationalTrialBalanceCategory.Partner),
            openingDebit: 50m,
            openingCredit: 0m,
            periodDebit: 40m,
            periodCredit: 10m,
            closingDebit: 80m,
            closingCredit: 0m);
        AssertAmounts(
            Find(result.Value, OperationalTrialBalanceCategory.Driver),
            openingDebit: 25m,
            openingCredit: 0m,
            periodDebit: 15m,
            periodCredit: 10m,
            closingDebit: 30m,
            closingCredit: 0m);
        AssertAmounts(
            Find(result.Value, OperationalTrialBalanceCategory.Employee),
            openingDebit: 0m,
            openingCredit: 100m,
            periodDebit: 35m,
            periodCredit: 0m,
            closingDebit: 0m,
            closingCredit: 65m);
        AssertAmounts(
            Find(result.Value, OperationalTrialBalanceCategory.Revenue),
            openingDebit: 0m,
            openingCredit: 40m,
            periodDebit: 5m,
            periodCredit: 60m,
            closingDebit: 0m,
            closingCredit: 95m);
        AssertAmounts(
            Find(result.Value, OperationalTrialBalanceCategory.Expense),
            openingDebit: 30m,
            openingCredit: 0m,
            periodDebit: 26m,
            periodCredit: 2m,
            closingDebit: 54m,
            closingCredit: 0m);

        Assert.Equal(140m, result.Value.Totals.OpeningDebit);
        Assert.Equal(140m, result.Value.Totals.OpeningCredit);
        Assert.Equal(187m, result.Value.Totals.PeriodDebit);
        Assert.Equal(177m, result.Value.Totals.PeriodCredit);
        Assert.Equal(170m, result.Value.Totals.ClosingDebit);
        Assert.Equal(160m, result.Value.Totals.ClosingCredit);
    }

    [Fact]
    public async Task SummaryAndCategoryFiltersPreserveDetailedColumns()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await SeedReportDataAsync(database);

        var service = database.CreateStatementService(1);
        var detailed = await service.GetOperationalTrialBalanceAsync(
            new OperationalTrialBalanceFilterRequest(
                FromDate: FromDate,
                ToDate: ToDate));
        var summary = await service.GetOperationalTrialBalanceAsync(
            new OperationalTrialBalanceFilterRequest(
                FromDate: FromDate,
                ToDate: ToDate,
                ViewMode: OperationalTrialBalanceViewMode.Summary));
        var expenses = await service.GetOperationalTrialBalanceAsync(
            new OperationalTrialBalanceFilterRequest(
                FromDate: FromDate,
                ToDate: ToDate,
                Category: OperationalTrialBalanceCategory.Expense,
                IncludeZeroBalances: true));

        Assert.True(detailed.IsSuccess);
        Assert.True(summary.IsSuccess);
        Assert.True(expenses.IsSuccess);
        Assert.Equal(6, summary.Value.Items.Count);
        Assert.All(summary.Value.Items, item =>
        {
            Assert.Null(item.AccountId);
            Assert.Null(item.AccountCode);
            Assert.Equal(item.CategoryName, item.AccountName);
        });

        Assert.Equal(
            detailed.Value.Totals,
            summary.Value.Totals);
        var detailedDriver = Find(
            detailed.Value,
            OperationalTrialBalanceCategory.Driver);
        var summaryDriver = Find(
            summary.Value,
            OperationalTrialBalanceCategory.Driver);
        AssertAmounts(
            summaryDriver,
            openingDebit: detailedDriver.OpeningDebit,
            openingCredit: detailedDriver.OpeningCredit,
            periodDebit: detailedDriver.PeriodDebit,
            periodCredit: detailedDriver.PeriodCredit,
            closingDebit: detailedDriver.ClosingDebit,
            closingCredit: detailedDriver.ClosingCredit);

        Assert.Equal(2, expenses.Value.Items.Count);
        Assert.All(
            expenses.Value.Items,
            item => Assert.Equal(
                OperationalTrialBalanceCategory.Expense,
                item.Category));
        var zeroExpense = Assert.Single(
            expenses.Value.Items,
            item => item.AccountId == 11);
        AssertAmounts(
            zeroExpense,
            openingDebit: 0m,
            openingCredit: 0m,
            periodDebit: 0m,
            periodCredit: 0m,
            closingDebit: 0m,
            closingCredit: 0m);
    }

    [Fact]
    public async Task DirectExpenseAccountVoucherAppearsInExpenseCategory()
    {
        await using var database =
            await CashManagementTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO CashVouchers (
                Id, CompanyId, VoucherNumber, VoucherDate, Direction,
                CashboxId, CashMovementTypeId, AccountId, PartyType,
                Amount, Currency, ExchangeRate, BaseAmount, IsPosted,
                LastModifiedAt, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (901, 1, 'TB-901', '2026-06-15', 2, 1, NULL, 2, 1,
                 77, 1, 1, 77, 1, '2026-06-15', 'test', '2026-06-15', 'test', 0),
                (902, 1, 'TB-902', '2026-06-16', 2, 1, NULL, 2, 1,
                 999, 1, 1, 999, 0, '2026-06-16', 'test', '2026-06-16', 'test', 0);
            """);
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 4901,
            entryNumber: "JE-TB-901",
            entryDate: new DateOnly(2026, 6, 15),
            entryType: JournalEntryType.Automatic,
            sourceType: JournalEntrySourceType.CashVoucher,
            sourceId: 901,
            sourceNumber: "TB-901",
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 2,
                    PartyType: null,
                    PartyId: null,
                    Debit: 77m,
                    Credit: 0m,
                    Currency: CurrencyCode.EGP,
                    ExchangeRate: 1m,
                    TransactionDebit: 77m,
                    TransactionCredit: 0m)
            ]);
        await database.SeedPostedJournalEntryAsync(
            journalEntryId: 4902,
            entryNumber: "JE-TB-902",
            entryDate: new DateOnly(2026, 6, 16),
            entryType: JournalEntryType.Automatic,
            sourceType: JournalEntrySourceType.CashVoucher,
            sourceId: 902,
            sourceNumber: "TB-902",
            lines:
            [
                new CashManagementTestDatabase.JournalEntryLineSeed(
                    AccountId: 2,
                    PartyType: null,
                    PartyId: null,
                    Debit: 999m,
                    Credit: 0m,
                    Currency: CurrencyCode.EGP,
                    ExchangeRate: 1m,
                    TransactionDebit: 999m,
                    TransactionCredit: 0m)
            ],
            status: (JournalEntryStatus)0);
        database.Context.ChangeTracker.Clear();

        var result = await database.CreateStatementService(1)
            .GetOperationalTrialBalanceAsync(
                new OperationalTrialBalanceFilterRequest(
                    FromDate: FromDate,
                    ToDate: ToDate,
                    Category: OperationalTrialBalanceCategory.Expense,
                    IncludeZeroBalances: true));

        Assert.True(result.IsSuccess);
        var accountItem = Assert.Single(
            result.Value.Items,
            item => item.AccountId == 2 &&
                item.Category == OperationalTrialBalanceCategory.Expense);
        Assert.Equal(77m, accountItem.PeriodDebit);
        Assert.Equal(0m, accountItem.PeriodCredit);
        Assert.DoesNotContain(
            result.Value.Items,
            item => item.PeriodDebit == 999m || item.PeriodCredit == 999m);
    }

    [Fact]
    public void FilterValidatorRejectsInvalidDatesAndEnums()
    {
        IValidator<OperationalTrialBalanceFilterRequest> validator =
            new OperationalTrialBalanceFilterRequestValidator();

        var result = validator.Validate(
            new OperationalTrialBalanceFilterRequest(
                FromDate: new DateOnly(2026, 8, 2),
                ToDate: new DateOnly(2026, 8, 1),
                ViewMode: (OperationalTrialBalanceViewMode)99,
                Category: (OperationalTrialBalanceCategory)99));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "ToDate");
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "ViewMode");
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "Category");
    }

    private static OperationalTrialBalanceItemResponse Find(
        OperationalTrialBalanceResponse response,
        OperationalTrialBalanceCategory category) =>
        Assert.Single(response.Items, item => item.Category == category);

    private static void AssertAmounts(
        OperationalTrialBalanceItemResponse item,
        decimal openingDebit,
        decimal openingCredit,
        decimal periodDebit,
        decimal periodCredit,
        decimal closingDebit,
        decimal closingCredit)
    {
        Assert.Equal(openingDebit, item.OpeningDebit);
        Assert.Equal(openingCredit, item.OpeningCredit);
        Assert.Equal(periodDebit, item.PeriodDebit);
        Assert.Equal(periodCredit, item.PeriodCredit);
        Assert.Equal(closingDebit, item.ClosingDebit);
        Assert.Equal(closingCredit, item.ClosingCredit);
    }

    private static async Task SeedReportDataAsync(
        CashManagementTestDatabase database)
    {
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM EmployeeTransactions;
            DELETE FROM BusinessPartnerMovements;
            DELETE FROM PartnerOpeningBalances;
            DELETE FROM CashVouchers;
            DELETE FROM DriverTrips;

            INSERT INTO CompanySettings (CompanyId, BaseCurrency, StockBalanceCheckMode)
            VALUES (1, 2, 1);

            UPDATE Cashboxes
            SET BaseOpeningBalance = 0,
                OpeningBalanceDate = '2026-01-01';
            UPDATE Cashboxes
            SET BaseOpeningBalance = 100
            WHERE Id = 1;

            INSERT INTO CashMovementTypes (
                Id, CompanyId, Name, Direction, Classification, PartnerEffect,
                IsActive, IsDefaultForSales, IsDefaultForPurchase,
                IsDefaultForSalesReturn, IsDefaultForPurchaseReturn,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (
                11, 1, 'Unused Expense', 2, 2, 0,
                1, 0, 0, 0, 0,
                'test', '2026-01-01', 'test', 0);

            INSERT INTO Accounts (
                Id, CompanyId, Code, Name, ParentAccountId, AccountType,
                NormalBalance, IsPosting, IsActive, IsDeleted)
            VALUES
                (4, 1, '3000', 'Opening Equity', NULL, 3, 2, 1, 1, 0),
                (11, 1, '5201', 'Unused Expense Account', NULL, 5, 1, 1, 1, 0);

            INSERT INTO CashVouchers (
                Id, CompanyId, VoucherNumber, VoucherDate, Direction,
                CashboxId, CashMovementTypeId, PartyType, EmployeeId,
                BusinessPartnerId, DriverId, DriverTripId, ExternalPartyName,
                Amount, Currency, ExchangeRate, BaseAmount, IsPosted,
                LastModifiedAt, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (101, 1, 'TB-101', '2026-05-20', 1, 1, NULL, 1, NULL, NULL, NULL, NULL, NULL, 5, 2, 10, 50, 1, '2026-05-20', 'test', '2026-05-20', 'test', 0),
                (102, 1, 'TB-102', '2026-06-10', 2, 1, NULL, 1, NULL, NULL, NULL, NULL, NULL, 2, 2, 10, 20, 1, '2026-06-10', 'test', '2026-06-10', 'test', 0),
                (103, 1, 'TB-103', '2026-06-11', 1, 1, NULL, 1, NULL, NULL, NULL, NULL, NULL, 999, 1, 1, 999, 0, '2026-06-11', 'test', '2026-06-11', 'test', 0),
                (104, 1, 'TB-104', '2026-07-01', 1, 1, NULL, 1, NULL, NULL, NULL, NULL, NULL, 999, 1, 1, 999, 1, '2026-07-01', 'test', '2026-07-01', 'test', 0),
                (110, 1, 'TB-110', '2026-05-10', 2, 1, NULL, 3, NULL, NULL, 1, NULL, NULL, 3, 2, 10, 30, 1, '2026-05-10', 'test', '2026-05-10', 'test', 0),
                (111, 1, 'TB-111', '2026-05-11', 1, 1, NULL, 3, NULL, NULL, 1, NULL, NULL, 5, 1, 1, 5, 1, '2026-05-11', 'test', '2026-05-11', 'test', 0),
                (112, 1, 'TB-112', '2026-06-12', 2, 1, NULL, 3, NULL, NULL, 1, NULL, NULL, 15, 1, 1, 15, 1, '2026-06-12', 'test', '2026-06-12', 'test', 0),
                (113, 1, 'TB-113', '2026-06-13', 1, 1, NULL, 3, NULL, NULL, 1, NULL, NULL, 4, 1, 1, 4, 1, '2026-06-13', 'test', '2026-06-13', 'test', 0),
                (120, 1, 'TB-120', '2026-05-15', 2, 1, NULL, 5, 1, NULL, NULL, NULL, NULL, 100, 1, 1, 100, 1, '2026-05-15', 'test', '2026-05-15', 'test', 0),
                (121, 1, 'TB-121', '2026-06-15', 2, 1, NULL, 5, 1, NULL, NULL, NULL, NULL, 25, 1, 1, 25, 1, '2026-06-15', 'test', '2026-06-15', 'test', 0),
                (122, 1, 'TB-122', '2026-06-16', 2, 1, NULL, 5, 1, NULL, NULL, NULL, NULL, 10, 1, 1, 10, 1, '2026-06-16', 'test', '2026-06-16', 'test', 0),
                (130, 1, 'TB-130', '2026-05-01', 1, 1, 9, 1, NULL, NULL, NULL, NULL, NULL, 4, 2, 10, 40, 1, '2026-05-01', 'test', '2026-05-01', 'test', 0),
                (131, 1, 'TB-131', '2026-06-01', 1, 1, 9, 1, NULL, NULL, NULL, NULL, NULL, 6, 2, 10, 60, 1, '2026-06-01', 'test', '2026-06-01', 'test', 0),
                (132, 1, 'TB-132', '2026-06-02', 2, 1, 9, 1, NULL, NULL, NULL, NULL, NULL, 5, 1, 1, 5, 1, '2026-06-02', 'test', '2026-06-02', 'test', 0),
                (133, 1, 'TB-133', '2026-06-03', 1, 1, 9, 1, NULL, NULL, NULL, NULL, NULL, 500, 1, 1, 500, 0, '2026-06-03', 'test', '2026-06-03', 'test', 0),
                (140, 1, 'TB-140', '2026-05-01', 2, 1, 10, 1, NULL, NULL, NULL, NULL, NULL, 30, 1, 1, 30, 1, '2026-05-01', 'test', '2026-05-01', 'test', 0),
                (141, 1, 'TB-141', '2026-06-01', 2, 1, 10, 1, NULL, NULL, NULL, NULL, NULL, 20, 1, 1, 20, 1, '2026-06-01', 'test', '2026-06-01', 'test', 0),
                (142, 1, 'TB-142', '2026-06-02', 1, 1, 10, 1, NULL, NULL, NULL, NULL, NULL, 2, 1, 1, 2, 1, '2026-06-02', 'test', '2026-06-02', 'test', 0),
                (150, 2, 'TB-150', '2026-06-01', 1, 4, NULL, 1, NULL, NULL, NULL, NULL, NULL, 999, 1, 1, 999, 1, '2026-06-01', 'test', '2026-06-01', 'test', 0);

            INSERT INTO PartnerOpeningBalances (
                Id, CompanyId, BusinessPartnerId, DocumentNumber, DocumentDate,
                Currency, ExchangeRate, BalanceType, Amount, BaseAmount,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (101, 1, 1, 'TB-OPEN-1', '2026-05-01', 2, 10, 1, 7, 70, 'test', '2026-05-01', 'test', 0),
                (102, 2, 3, 'TB-OPEN-2', '2026-05-01', 1, 1, 1, 999, 999, 'test', '2026-05-01', 'test', 0);

            INSERT INTO BusinessPartnerMovements (
                Id, CompanyId, BusinessPartnerId, MovementType, MovementDate,
                Currency, Debit, Credit, ExchangeRate, BaseDebit, BaseCredit,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (101, 1, 1, 1, '2026-05-10', 1, 0, 20, 1, 0, 20, 'test', '2026-05-10', 'test', 0),
                (102, 1, 1, 1, '2026-06-10', 2, 4, 0, 10, 40, 0, 'test', '2026-06-10', 'test', 0),
                (103, 1, 1, 1, '2026-06-11', 1, 0, 10, 1, 0, 10, 'test', '2026-06-11', 'test', 0),
                (104, 1, 1, 1, '2026-07-01', 1, 500, 0, 1, 500, 0, 'test', '2026-07-01', 'test', 0),
                (105, 2, 3, 1, '2026-06-01', 1, 999, 0, 1, 999, 0, 'test', '2026-06-01', 'test', 0);

            INSERT INTO DriverTrips (
                Id, CompanyId, DriverId, InvoiceId, BusinessPartnerId,
                InvoiceNumber, TripDate, Cost,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (101, 1, 1, 1, 1, 'TB-INV-1', '2026-06-14', 6, 'test', '2026-06-14', 'test', 0),
                (102, 1, 1, 1, 1, 'TB-INV-2', '2026-07-01', 99, 'test', '2026-07-01', 'test', 0),
                (103, 2, 3, 3, 3, 'TB-INV-3', '2026-06-14', 999, 'test', '2026-06-14', 'test', 0);

            INSERT INTO EmployeeTransactions (
                Id, CompanyId, EmployeeId, Type, Amount, TransactionDate,
                RunningBalance, SourceType, CashVoucherId, CashBoxId,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (101, 1, 1, 2, 100, '2026-05-15', 100, 1, 120, 1, 'test', '2026-05-15', 'test', 0),
                (102, 1, 1, 1, 25, '2026-06-15', 75, 1, 121, 1, 'test', '2026-06-15', 'test', 0);
            """);

        await database.Context.Database.ExecuteSqlRawAsync(
            "DELETE FROM JournalEntryLines; DELETE FROM JournalEntries;");
        await SeedTrialBalanceLedgerAsync(database);
        database.Context.ChangeTracker.Clear();
    }

    private static async Task SeedTrialBalanceLedgerAsync(
        CashManagementTestDatabase database)
    {
        async Task Entry(
            int id,
            DateOnly date,
            JournalEntryType type,
            JournalEntrySourceType? sourceType,
            int? sourceId,
            string number,
            IReadOnlyList<CashManagementTestDatabase.JournalEntryLineSeed> lines,
            JournalEntryStatus status = JournalEntryStatus.Posted,
            int? reversalOfEntryId = null,
            bool isDeleted = false,
            int companyId = 1) =>
            await database.SeedPostedJournalEntryAsync(
                id,
                $"JE-{number}",
                date,
                type,
                sourceType,
                sourceId,
                number,
                lines,
                status,
                reversalOfEntryId,
                isDeleted,
                companyId);

        static CashManagementTestDatabase.JournalEntryLineSeed Line(
            int accountId,
            JournalPartyType? partyType,
            int? partyId,
            decimal debit,
            decimal credit,
            CurrencyCode currency = CurrencyCode.EGP,
            decimal rate = 1m,
            decimal? transactionDebit = null,
            decimal? transactionCredit = null,
            string? description = null,
            bool isDeleted = false) =>
            new(
                AccountId: accountId,
                PartyType: partyType,
                PartyId: partyId,
                Debit: debit,
                Credit: credit,
                Currency: currency,
                ExchangeRate: rate,
                TransactionDebit: transactionDebit ?? debit,
                TransactionCredit: transactionCredit ?? credit,
                Description: description,
                IsDeleted: isDeleted);

        const JournalPartyType cashbox = JournalPartyType.Cashbox;
        const JournalPartyType customer = JournalPartyType.Customer;
        const JournalPartyType driver = JournalPartyType.Driver;
        const JournalPartyType employee = JournalPartyType.Employee;
        var voucherSource = JournalEntrySourceType.CashVoucher;

        await Entry(
            4100, new DateOnly(2026, 1, 1), JournalEntryType.Opening,
            JournalEntrySourceType.CashboxOpeningBalance, 1, "OPEN-CB",
            [Line(4, cashbox, 1, 100m, 0m)]);

        await Entry(4101, new DateOnly(2026, 5, 20), JournalEntryType.Automatic,
            voucherSource, 101, "TB-101",
            [Line(4, cashbox, 1, 50m, 0m, CurrencyCode.USD, 10m, 5m, 0m)]);
        await Entry(4110, new DateOnly(2026, 5, 10), JournalEntryType.Automatic,
            voucherSource, 110, "TB-110",
            [Line(4, cashbox, 1, 0m, 30m, CurrencyCode.USD, 10m, 0m, 3m),
             Line(4, driver, 1, 30m, 0m, CurrencyCode.USD, 10m, 3m, 0m)]);
        await Entry(4111, new DateOnly(2026, 5, 11), JournalEntryType.Automatic,
            voucherSource, 111, "TB-111",
            [Line(4, cashbox, 1, 5m, 0m), Line(4, driver, 1, 0m, 5m)]);
        await Entry(4120, new DateOnly(2026, 5, 15), JournalEntryType.Automatic,
            voucherSource, 120, "TB-120",
            [Line(4, cashbox, 1, 0m, 100m), Line(4, employee, 1, 0m, 100m)]);
        await Entry(4130, new DateOnly(2026, 5, 1), JournalEntryType.Automatic,
            voucherSource, 130, "TB-130",
            [Line(4, cashbox, 1, 40m, 0m, CurrencyCode.USD, 10m, 4m, 0m),
             Line(1, null, null, 0m, 40m, CurrencyCode.USD, 10m, 0m, 4m)]);
        await Entry(4140, new DateOnly(2026, 5, 1), JournalEntryType.Automatic,
            voucherSource, 140, "TB-140",
            [Line(4, cashbox, 1, 0m, 30m), Line(2, null, null, 30m, 0m)]);

        await Entry(4201, new DateOnly(2026, 6, 10), JournalEntryType.Automatic,
            voucherSource, 102, "TB-102",
            [Line(4, cashbox, 1, 0m, 20m, CurrencyCode.USD, 10m, 0m, 2m)]);
        await Entry(4212, new DateOnly(2026, 6, 12), JournalEntryType.Automatic,
            voucherSource, 112, "TB-112",
            [Line(4, cashbox, 1, 0m, 15m), Line(4, driver, 1, 15m, 0m)]);
        await Entry(4213, new DateOnly(2026, 6, 13), JournalEntryType.Automatic,
            voucherSource, 113, "TB-113",
            [Line(4, cashbox, 1, 4m, 0m), Line(4, driver, 1, 0m, 4m)]);
        await Entry(4221, new DateOnly(2026, 6, 15), JournalEntryType.Automatic,
            voucherSource, 121, "TB-121",
            [Line(4, cashbox, 1, 0m, 25m), Line(4, employee, 1, 25m, 0m)]);
        await Entry(4222, new DateOnly(2026, 6, 16), JournalEntryType.Automatic,
            voucherSource, 122, "TB-122",
            [Line(4, cashbox, 1, 0m, 10m), Line(4, employee, 1, 10m, 0m)]);
        await Entry(4231, new DateOnly(2026, 6, 1), JournalEntryType.Automatic,
            voucherSource, 131, "TB-131",
            [Line(4, cashbox, 1, 60m, 0m, CurrencyCode.USD, 10m, 6m, 0m),
             Line(1, null, null, 0m, 60m, CurrencyCode.USD, 10m, 0m, 6m)]);
        await Entry(4232, new DateOnly(2026, 6, 2), JournalEntryType.Automatic,
            voucherSource, 132, "TB-132",
            [Line(4, cashbox, 1, 0m, 5m), Line(1, null, null, 5m, 0m)]);
        await Entry(4241, new DateOnly(2026, 6, 1), JournalEntryType.Automatic,
            voucherSource, 141, "TB-141",
            [Line(4, cashbox, 1, 0m, 20m), Line(2, null, null, 20m, 0m)]);
        await Entry(4242, new DateOnly(2026, 6, 2), JournalEntryType.Automatic,
            voucherSource, 142, "TB-142",
            [Line(4, cashbox, 1, 2m, 0m), Line(2, null, null, 0m, 2m)]);

        await Entry(4150, new DateOnly(2026, 5, 1), JournalEntryType.Opening,
            JournalEntrySourceType.PartnerOpeningBalance, 101, "TB-OPEN-1",
            [Line(4, customer, 1, 70m, 0m, CurrencyCode.USD, 10m, 7m, 0m)]);
        await Entry(4151, new DateOnly(2026, 5, 10), JournalEntryType.Automatic,
            JournalEntrySourceType.Invoice, 1, "TB-PARTNER-101",
            [Line(4, customer, 1, 0m, 20m)]);
        await Entry(4152, new DateOnly(2026, 6, 10), JournalEntryType.Automatic,
            JournalEntrySourceType.Invoice, 2, "TB-PARTNER-102",
            [Line(4, customer, 1, 40m, 0m, CurrencyCode.USD, 10m, 4m, 0m)]);
        await Entry(4153, new DateOnly(2026, 6, 11), JournalEntryType.Automatic,
            JournalEntrySourceType.Invoice, 3, "TB-PARTNER-103",
            [Line(4, customer, 1, 0m, 10m)]);

        await Entry(4250, new DateOnly(2026, 6, 14), JournalEntryType.Automatic,
            JournalEntrySourceType.DriverTrip, 101, "TB-INV-1",
            [Line(4, driver, 1, 0m, 6m), Line(2, null, null, 6m, 0m)]);

        await Entry(4199, new DateOnly(2026, 6, 20), JournalEntryType.Manual,
            null, null, "TB-DELETED",
            [Line(2, null, null, 999m, 0m)], isDeleted: true);
        await Entry(4198, new DateOnly(2026, 6, 20), JournalEntryType.Manual,
            null, null, "TB-DELETED-LINE",
            [Line(2, null, null, 999m, 0m, isDeleted: true)]);
        await Entry(4197, new DateOnly(2026, 6, 20), JournalEntryType.Manual,
            null, null, "TB-REVERSED",
            [Line(2, null, null, 999m, 0m)],
            reversalOfEntryId: 4100);
        await Entry(4103, new DateOnly(2026, 6, 11), JournalEntryType.Automatic,
            voucherSource, 103, "TB-103",
            [Line(4, cashbox, 1, 999m, 0m)],
            status: (JournalEntryStatus)0);
        await Entry(4104, new DateOnly(2026, 7, 1), JournalEntryType.Automatic,
            voucherSource, 104, "TB-104",
            [Line(4, cashbox, 1, 999m, 0m)]);
        await Entry(4155, new DateOnly(2026, 6, 1), JournalEntryType.Automatic,
            voucherSource, 150, "TB-150",
            [Line(4, cashbox, 4, 999m, 0m)], companyId: 2);
    }
}
