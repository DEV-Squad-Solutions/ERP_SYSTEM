using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Statements;

namespace MiniErp.Tests.Accounting;

public sealed class FinancialStatementReportTests
{
    [Fact]
    public async Task TrialBalance_ReadsOnlyJournalLinesAndBalancesEveryBucket()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Service.GetTrialBalanceAsync(
            new TrialBalanceFilterRequest(
                FromDate: new DateOnly(2026, 2, 1),
                ToDate: new DateOnly(2026, 12, 31),
                FiscalYearId: 1,
                ViewMode: TrialBalanceViewMode.Detailed,
                AdjustmentView: TrialBalanceAdjustmentView.AfterAdjustments));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsOperationalOnly);
        Assert.Equal(6, result.Value.Items.Count);
        Assert.DoesNotContain(result.Value.Items, item => item.IsUnclassified);
        Assert.Equal(1000m, result.Value.Totals.OpeningDebit);
        Assert.Equal(1000m, result.Value.Totals.OpeningCredit);
        Assert.Equal(1600m, result.Value.Totals.PeriodDebit);
        Assert.Equal(1600m, result.Value.Totals.PeriodCredit);
        Assert.Equal(2200m, result.Value.Totals.ClosingDebit);
        Assert.Equal(2200m, result.Value.Totals.ClosingCredit);
    }

    [Fact]
    public async Task IncomeAndFinancialPosition_UseLedgerAndSubtractExpenses()
    {
        await using var database = await TestDatabase.CreateAsync();
        var request = CreateRequest();

        var incomeStatement = await database.Service
            .GetFinancialStatementReportAsync(
                FinancialStatementType.IncomeStatement,
                request);
        var financialPosition = await database.Service
            .GetFinancialStatementReportAsync(
                FinancialStatementType.FinancialPosition,
                request);

        Assert.True(incomeStatement.IsSuccess);
        Assert.True(incomeStatement.Value.IsReadyForReporting);
        Assert.Equal(900m, incomeStatement.Value.Totals.NetResult);
        Assert.Equal(1200m, incomeStatement.Value.Items
            .Where(item => item.AccountType == AccountType.Revenue)
            .Sum(item => item.PeriodCredit - item.PeriodDebit));
        Assert.Equal(300m, incomeStatement.Value.Items
            .Where(item => item.AccountType == AccountType.Expense)
            .Sum(item => item.PeriodDebit - item.PeriodCredit));

        Assert.True(financialPosition.IsSuccess);
        Assert.True(financialPosition.Value.IsReadyForReporting);
        Assert.Equal(900m, financialPosition.Value.Totals.NetResult);
        Assert.Equal(1900m, financialPosition.Value.Totals.TotalAssets);
        Assert.Equal(
            1900m,
            financialPosition.Value.Totals.TotalLiabilitiesAndEquity);
        Assert.True(financialPosition.Value.Totals.IsBalanced);
    }

    [Fact]
    public async Task CashFlow_OnlyRequiresMappingsForCashCounterparts()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Service.GetFinancialStatementReportAsync(
            FinancialStatementType.CashFlow,
            CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsReadyForReporting);
        Assert.Equal(600m, result.Value.Totals.NetCashFlow);
        var unmapped = Assert.Single(result.Value.UnmappedAccounts);
        Assert.Equal("1300", unmapped.AccountCode);
        Assert.Equal(0m, unmapped.PeriodDebit);
        Assert.Equal(100m, unmapped.PeriodCredit);
        Assert.DoesNotContain(
            result.Value.UnmappedAccounts,
            item => item.AccountCode == "1200");

        database.Context.AccountStatementMappings.Add(
            new AccountStatementMapping
            {
                Id = 10,
                CompanyId = 1,
                FiscalYearId = 1,
                StatementType = FinancialStatementType.CashFlow,
                AccountId = TestDatabase.InventoryAccountId,
                FinancialStatementLineId = TestDatabase.InvestingLineId
            });
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var mappedResult = await database.Service
            .GetFinancialStatementReportAsync(
                FinancialStatementType.CashFlow,
                CreateRequest());

        Assert.True(mappedResult.IsSuccess);
        Assert.True(mappedResult.Value.IsReadyForReporting);
        Assert.Empty(mappedResult.Value.UnmappedAccounts);
        Assert.Equal(600m, mappedResult.Value.Totals.NetCashFlow);
        var investing = Assert.Single(
            mappedResult.Value.Items,
            item => item.FinancialStatementLineCode == "CF-210");
        Assert.Equal(100m, investing.PeriodCredit);
    }

    [Fact]
    public async Task IncomeStatement_SeparatesBeforeAndAfterAdjustments()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO JournalEntries (
                Id, CompanyId, FiscalYearId, EntryNumber, EntryDate,
                Description, EntryType, SourceType, SourceId, SourceNumber,
                Status, PostedOn, RowVersion, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES (
                6, 1, 1, 'JE-006', '2026-12-20', 'Year-end adjustment', 2,
                NULL, NULL, NULL, 1, '2026-12-20', randomblob(8),
                'test', '2026-12-20', 'test', 0);

            INSERT INTO JournalEntryLines (
                Id, CompanyId, JournalEntryId, AccountId, Description,
                Debit, Credit, CreatedById, CreatedOn, CreatedByPc,
                IsDeleted)
            VALUES
                (60, 1, 6, 6, NULL, 50, 0,
                 'test', '2026-12-20', 'test', 0),
                (61, 1, 6, 1, NULL, 0, 50,
                 'test', '2026-12-20', 'test', 0);
            """);

        var before = await database.Service.GetFinancialStatementReportAsync(
            FinancialStatementType.IncomeStatement,
            CreateRequest() with
            {
                AdjustmentView = TrialBalanceAdjustmentView.BeforeAdjustments
            });
        var after = await database.Service.GetFinancialStatementReportAsync(
            FinancialStatementType.IncomeStatement,
            CreateRequest() with
            {
                AdjustmentView = TrialBalanceAdjustmentView.AfterAdjustments
            });

        Assert.True(before.IsSuccess);
        Assert.True(after.IsSuccess);
        Assert.Equal(900m, before.Value.Totals.NetResult);
        Assert.Equal(850m, after.Value.Totals.NetResult);
    }

    [Fact]
    public async Task FinancialReports_AreNotReadyWhileInventoryCostIsPending()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            PRAGMA foreign_keys = OFF;
            INSERT INTO ItemMovements (
                Id, CompanyId, StoreId, ItemId, ItemUnitId, MovementType,
                ReferenceId, ReferenceNumber, MovementDate, QuantityIn,
                QuantityOut, CostStatus, PendingCostQuantity, UnitCost,
                TotalCost, QuantityAfter, AverageCostAfter,
                InventoryValueAfter, Description, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES (
                1, 1, 1, 1, NULL, 1, 999, 'PENDING-001', '2026-08-01',
                0, 1, 3, 1, NULL, 0, 0, 0, 0, NULL,
                'test', '2026-08-01', 'test', 0);
            PRAGMA foreign_keys = ON;
            """);

        var result = await database.Service.GetFinancialStatementReportAsync(
            FinancialStatementType.IncomeStatement,
            CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.UnmappedAccounts);
        Assert.True(result.Value.Totals.IsBalanced);
        Assert.False(result.Value.IsReadyForReporting);
    }

    [Fact]
    public async Task FinancialReports_IgnoreReversedAndReversalEntries()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO JournalEntries (
                Id, CompanyId, FiscalYearId, EntryNumber, EntryDate,
                Description, EntryType, SourceType, SourceId, SourceNumber,
                Status, PostedOn, ReversedOn, ReversalOfEntryId, RowVersion,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (7, 1, 1, 'JE-007', '2026-10-01', 'Legacy reversed', 1,
                 NULL, NULL, NULL, 2, '2026-10-01', '2026-10-02', NULL, randomblob(8),
                 'test', '2026-10-01', 'test', 0),
                (8, 1, 1, 'JE-008', '2026-10-02', 'Legacy reversal', 1,
                 NULL, NULL, NULL, 1, '2026-10-02', NULL, 7, randomblob(8),
                 'test', '2026-10-02', 'test', 0);

            INSERT INTO JournalEntryLines (
                Id, CompanyId, JournalEntryId, AccountId, Description,
                Debit, Credit, CreatedById, CreatedOn, CreatedByPc,
                IsDeleted)
            VALUES
                (70, 1, 7, 1, NULL, 999, 0,
                 'test', '2026-10-01', 'test', 0),
                (71, 1, 7, 5, NULL, 0, 999,
                 'test', '2026-10-01', 'test', 0),
                (80, 1, 8, 5, NULL, 999, 0,
                 'test', '2026-10-02', 'test', 0),
                (81, 1, 8, 1, NULL, 0, 999,
                 'test', '2026-10-02', 'test', 0);
            """);

        var trialBalance = await database.Service.GetTrialBalanceAsync(
            new TrialBalanceFilterRequest(
                FromDate: new DateOnly(2026, 2, 1),
                ToDate: new DateOnly(2026, 12, 31),
                FiscalYearId: 1,
                AdjustmentView:
                    TrialBalanceAdjustmentView.AfterAdjustments));
        var income = await database.Service.GetFinancialStatementReportAsync(
            FinancialStatementType.IncomeStatement,
            CreateRequest());

        Assert.True(trialBalance.IsSuccess);
        Assert.Equal(1600m, trialBalance.Value.Totals.PeriodDebit);
        Assert.Equal(1600m, trialBalance.Value.Totals.PeriodCredit);
        Assert.True(income.IsSuccess);
        Assert.Equal(900m, income.Value.Totals.NetResult);
    }

    private static FinancialStatementReportRequest CreateRequest() =>
        new(
            FromDate: new DateOnly(2026, 2, 1),
            ToDate: new DateOnly(2026, 12, 31),
            FiscalYearId: 1,
            ViewMode: TrialBalanceViewMode.Detailed,
            AdjustmentView: TrialBalanceAdjustmentView.AfterAdjustments,
            IncludeUnmapped: true);

    private sealed class TestDatabase : IAsyncDisposable
    {
        public const int InventoryAccountId = 3;
        public const int InvestingLineId = 32;

        private readonly SqliteConnection connection;

        private TestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            this.connection = connection;
            Context = context;
            Service = new FinancialStatementService(
                context,
                new TestCurrentCompanyContext(1));
        }

        public ApplicationDbContext Context { get; }

        public FinancialStatementService Service { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            await SeedAsync(context);
            context.ChangeTracker.Clear();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }

        private static async Task SeedAsync(ApplicationDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Companies (
                    Id, Name, Address, CommercialRegister, TaxNumber,
                    ManagerName, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES (
                    1, 'Financial Reports Test Company', 'Test Address',
                    'CR-REPORTS', 'TAX-REPORTS', 'Test Manager', randomblob(8),
                    'test', '2026-01-01', 'test', 0);

                INSERT INTO CompanySettings (
                    CompanyId, BaseCurrency, StockBalanceCheckMode)
                VALUES (1, 1, 1);

                INSERT INTO FiscalYears (
                    Id, CompanyId, Name, StartDate, EndDate, Status, IsCurrent,
                    RowVersion, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    1, 1, '2026', '2026-01-01', '2026-12-31', 1, 1,
                    randomblob(8), 'test', '2026-01-01', 'test', 0);

                INSERT INTO Accounts (
                    Id, CompanyId, Code, Name, ParentAccountId, AccountType,
                    NormalBalance, IsPosting, IsActive, RowVersion,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, '1110', 'Cash', NULL, 1, 1, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0),
                    (2, 1, '1200', 'Customers', NULL, 1, 1, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0),
                    (3, 1, '1300', 'Inventory', NULL, 1, 1, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0),
                    (4, 1, '3100', 'Capital', NULL, 3, 2, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0),
                    (5, 1, '4100', 'Sales revenue', NULL, 4, 2, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0),
                    (6, 1, '5100', 'Operating expense', NULL, 5, 1, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0);

                INSERT INTO FinancialStatementLines (
                    Id, CompanyId, FiscalYearId, StatementType, Code, Name,
                    ParentLineId, DisplayOrder, IsAssignable, IsActive,
                    RowVersion, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (11, 1, 1, 1, 'FP-110', 'Assets', NULL, 11, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0),
                    (12, 1, 1, 1, 'FP-310', 'Equity', NULL, 12, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0),
                    (21, 1, 1, 2, 'IS-110', 'Revenue', NULL, 21, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0),
                    (22, 1, 1, 2, 'IS-210', 'Expenses', NULL, 22, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0),
                    (30, 1, 1, 3, 'CF-110', 'Operating receipts', NULL, 30, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0),
                    (31, 1, 1, 3, 'CF-120', 'Operating payments', NULL, 31, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0),
                    (32, 1, 1, 3, 'CF-210', 'Investing flows', NULL, 32, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0);

                INSERT INTO AccountMappings (
                    Id, CompanyId, FiscalYearId, MappingType, SourceId,
                    AccountId, RowVersion, CreatedById, CreatedOn, CreatedByPc,
                    IsDeleted)
                VALUES (
                    1, 1, 1, 1, 1, 1, randomblob(8),
                    'test', '2026-01-01', 'test', 0);

                INSERT INTO AccountStatementMappings (
                    Id, CompanyId, FiscalYearId, StatementType, AccountId,
                    FinancialStatementLineId, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 1, 1, 1, 11, 'test', '2026-01-01', 'test', 0),
                    (2, 1, 1, 1, 2, 11, 'test', '2026-01-01', 'test', 0),
                    (3, 1, 1, 1, 3, 11, 'test', '2026-01-01', 'test', 0),
                    (4, 1, 1, 1, 4, 12, 'test', '2026-01-01', 'test', 0),
                    (5, 1, 1, 2, 5, 21, 'test', '2026-01-01', 'test', 0),
                    (6, 1, 1, 2, 6, 22, 'test', '2026-01-01', 'test', 0),
                    (7, 1, 1, 3, 5, 30, 'test', '2026-01-01', 'test', 0),
                    (8, 1, 1, 3, 6, 31, 'test', '2026-01-01', 'test', 0);

                INSERT INTO JournalEntries (
                    Id, CompanyId, FiscalYearId, EntryNumber, EntryDate,
                    Description, EntryType, SourceType, SourceId, SourceNumber,
                    Status, PostedOn, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 1, 'JE-001', '2026-01-01', 'Opening', 3,
                     NULL, NULL, NULL, 1, '2026-01-01', randomblob(8),
                     'test', '2026-01-01', 'test', 0),
                    (2, 1, 1, 'JE-002', '2026-07-01', 'Cash sale', 1,
                     NULL, NULL, NULL, 1, '2026-07-01', randomblob(8),
                     'test', '2026-07-01', 'test', 0),
                    (3, 1, 1, 'JE-003', '2026-07-02', 'Cash expense', 1,
                     NULL, NULL, NULL, 1, '2026-07-02', randomblob(8),
                     'test', '2026-07-02', 'test', 0),
                    (4, 1, 1, 'JE-004', '2026-07-03', 'Credit sale', 1,
                     NULL, NULL, NULL, 1, '2026-07-03', randomblob(8),
                     'test', '2026-07-03', 'test', 0),
                    (5, 1, 1, 'JE-005', '2026-07-04', 'Inventory purchase', 1,
                     NULL, NULL, NULL, 1, '2026-07-04', randomblob(8),
                     'test', '2026-07-04', 'test', 0);

                INSERT INTO JournalEntryLines (
                    Id, CompanyId, JournalEntryId, AccountId, Description,
                    Debit, Credit, CreatedById, CreatedOn, CreatedByPc,
                    IsDeleted)
                VALUES
                    (10, 1, 1, 1, NULL, 1000, 0, 'test', '2026-01-01', 'test', 0),
                    (11, 1, 1, 4, NULL, 0, 1000, 'test', '2026-01-01', 'test', 0),
                    (20, 1, 2, 1, NULL, 1000, 0, 'test', '2026-07-01', 'test', 0),
                    (21, 1, 2, 5, NULL, 0, 1000, 'test', '2026-07-01', 'test', 0),
                    (30, 1, 3, 6, NULL, 300, 0, 'test', '2026-07-02', 'test', 0),
                    (31, 1, 3, 1, NULL, 0, 300, 'test', '2026-07-02', 'test', 0),
                    (40, 1, 4, 2, NULL, 200, 0, 'test', '2026-07-03', 'test', 0),
                    (41, 1, 4, 5, NULL, 0, 200, 'test', '2026-07-03', 'test', 0),
                    (50, 1, 5, 3, NULL, 100, 0, 'test', '2026-07-04', 'test', 0),
                    (51, 1, 5, 1, NULL, 0, 100, 'test', '2026-07-04', 'test', 0);
                """);
        }

        private sealed record TestCurrentCompanyContext(int CompanyId)
            : ICurrentCompanyContext;
    }
}
