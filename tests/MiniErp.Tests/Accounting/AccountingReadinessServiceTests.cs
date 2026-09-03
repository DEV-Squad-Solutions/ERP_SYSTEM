using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.AccountingReadiness;
using MiniErp.Infrastructure.Services.AccountMappings;
using MiniErp.Infrastructure.Services.CashboxTransfers;
using MiniErp.Infrastructure.Services.CashVouchers;
using MiniErp.Infrastructure.Services.DriverTrips;
using MiniErp.Infrastructure.Services.Invoices;
using MiniErp.Infrastructure.Services.JournalEntries;

namespace MiniErp.Tests.Accounting;

public sealed class AccountingReadinessServiceTests
{
    [Fact]
    public async Task Backfill_WithNoSources_IsIdempotentAndReady()
    {
        await using var database = await TestDatabase.CreateAsync();

        var first = await database.Service.BackfillAsync(1);
        var second = await database.Service.BackfillAsync(1);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(0, first.Value.ProcessedSources);
        Assert.Equal(0, first.Value.CreatedJournals);
        Assert.Equal(0, first.Value.UpdatedJournals);
        Assert.True(first.Value.Readiness.IsReady);
        Assert.True(second.Value.Readiness.IsReady);
        Assert.Empty(second.Value.Readiness.Issues);
    }

    [Fact]
    public async Task Readiness_FlagsOrphanAndEveryUnbalancedPostedEntry()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Accounts (
                Id, CompanyId, Code, Name, ParentAccountId, AccountType,
                NormalBalance, IsPosting, IsActive, RowVersion,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, '1110', 'Debit account', NULL, 1, 1, 1, 1,
                 randomblob(8), 'test', '2026-01-01', 'test', 0),
                (2, 1, '3100', 'Credit account', NULL, 3, 2, 1, 1,
                 randomblob(8), 'test', '2026-01-01', 'test', 0);

            INSERT INTO JournalEntries (
                Id, CompanyId, FiscalYearId, EntryNumber, EntryDate,
                Description, EntryType, SourceType, SourceId, SourceNumber,
                Status, PostedOn, RowVersion, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 1, 'JV-ORPHAN', '2026-03-01', 'Orphan automatic', 4,
                 1, 999, 'INV-999', 1, '2026-03-01', randomblob(8),
                 'test', '2026-03-01', 'test', 0),
                (2, 1, 1, 'JV-UNBALANCED', '2026-03-02', 'Manual imbalance', 1,
                 NULL, NULL, NULL, 1, '2026-03-02', randomblob(8),
                 'test', '2026-03-02', 'test', 0);

            INSERT INTO JournalEntryLines (
                Id, CompanyId, JournalEntryId, AccountId, Description,
                Debit, Credit, CreatedById, CreatedOn, CreatedByPc,
                IsDeleted)
            VALUES
                (1, 1, 1, 1, NULL, 100, 0,
                 'test', '2026-03-01', 'test', 0),
                (2, 1, 1, 2, NULL, 0, 100,
                 'test', '2026-03-01', 'test', 0),
                (3, 1, 2, 1, NULL, 25, 0,
                 'test', '2026-03-02', 'test', 0);
            """);

        var result = await database.Service.GetAsync(1);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsReady);
        Assert.Equal(1, result.Value.OrphanAutomaticJournals);
        Assert.Equal(1, result.Value.UnbalancedAutomaticJournals);
        Assert.Contains(result.Value.Issues, issue =>
            issue.IssueType == "OrphanJournal" &&
            issue.SourceId == 999);
        Assert.Contains(result.Value.Issues, issue =>
            issue.IssueType == "UnbalancedJournal" &&
            issue.SourceNumber == "JV-UNBALANCED");
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context,
            AccountingReadinessService service)
        {
            this.connection = connection;
            Context = context;
            Service = service;
        }

        public ApplicationDbContext Context { get; }

        public AccountingReadinessService Service { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Companies (
                    Id, Name, Address, CommercialRegister, TaxNumber,
                    ManagerName, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES (
                    1, 'Readiness Test Company', 'Test Address',
                    'CR-READINESS', 'TAX-READINESS', 'Test Manager',
                    randomblob(8), 'test', '2026-01-01', 'test', 0);

                INSERT INTO FiscalYears (
                    Id, CompanyId, Name, StartDate, EndDate, Status, IsCurrent,
                    RowVersion, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    1, 1, '2026', '2026-01-01', '2026-12-31', 1, 1,
                    randomblob(8), 'test', '2026-01-01', 'test', 0);
                """);

            var currentCompany = new TestCurrentCompanyContext(1);
            var resolver = new AccountMappingResolver(context, currentCompany);
            var automaticPosting = new AutomaticPostingService(
                context,
                currentCompany,
                TimeProvider.System,
                NullLogger<AutomaticPostingService>.Instance);
            var service = new AccountingReadinessService(
                context,
                currentCompany,
                new NoOpInventoryCostingService(),
                new InvoicePostingService(
                    context,
                    currentCompany,
                    resolver,
                    automaticPosting),
                new CashVoucherPostingService(
                    context,
                    currentCompany,
                    resolver,
                    automaticPosting),
                new CashboxTransferPostingService(
                    context,
                    currentCompany,
                    resolver,
                    automaticPosting),
                new InventoryPostingService(
                    context,
                    currentCompany,
                    resolver,
                    automaticPosting),
                new OpeningBalancePostingService(
                    context,
                    currentCompany,
                    resolver,
                    automaticPosting),
                new DriverTripPostingService(
                    context,
                    currentCompany,
                    resolver,
                    automaticPosting),
                NullLogger<AccountingReadinessService>.Instance);
            return new TestDatabase(connection, context, service);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }

        private sealed record TestCurrentCompanyContext(int CompanyId)
            : ICurrentCompanyContext;

        private sealed class NoOpInventoryCostingService
            : IInventoryCostingService
        {
            public Task LockAsync(
                IReadOnlyCollection<InventoryCostingKey> keys,
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task<Error?> RecalculateAsync(
                IReadOnlyCollection<InventoryCostingKey> keys,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<Error?>(null);

            public Task<IReadOnlyDictionary<int, InventoryCostSnapshot>>
                GetSnapshotsAsync(
                    int storeId,
                    IReadOnlyCollection<int> itemIds,
                    DateOnly asOfDate,
                    CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyDictionary<int, InventoryCostSnapshot>>(
                    new Dictionary<int, InventoryCostSnapshot>());
        }
    }
}
