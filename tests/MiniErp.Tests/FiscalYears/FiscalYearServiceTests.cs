using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.FiscalYears;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.FiscalYears;

public sealed class FiscalYearServiceTests
{
    static FiscalYearServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task Add_TrimsNameAndMakesFirstYearCurrent()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new FiscalYearRequest(
                "  2026  ",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));

        Assert.True(result.IsSuccess);
        Assert.Equal("2026", result.Value.Name);
        Assert.Equal(FiscalYearStatus.Open, result.Value.Status);
        Assert.True(result.Value.IsCurrent);
    }

    [Fact]
    public async Task Add_RejectsOverlappingPeriodInSameCompany()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var first = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        var overlapping = await service.AddAsync(
            new FiscalYearRequest(
                "2026/2027",
                new DateOnly(2026, 12, 1),
                new DateOnly(2027, 11, 30),
                IsCurrent: false));

        Assert.True(first.IsSuccess);
        Assert.True(overlapping.IsFailure);
        Assert.Equal(
            "FiscalYears.DateRangeOverlaps",
            overlapping.Error.Code);
    }

    [Fact]
    public async Task CurrentYear_IsScopedPerCompanyAndCanBeSwitched()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var companyOne = database.CreateService(companyId: 1);
        var companyTwo = database.CreateService(companyId: 2);

        var first = await companyOne.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        var second = await companyOne.AddAsync(
            new FiscalYearRequest(
                "2027",
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                IsCurrent: true));
        var otherCompany = await companyTwo.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(otherCompany.IsSuccess);
        Assert.False((await companyOne.GetByIdAsync(first.Value.Id)).Value.IsCurrent);
        Assert.True((await companyOne.GetCurrentAsync()).Value.Id == second.Value.Id);
        Assert.True((await companyTwo.GetCurrentAsync()).Value.Id == otherCompany.Value.Id);
    }

    [Fact]
    public async Task Update_UsesRowVersionAndClosedYearCannotBeModified()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var added = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));

        var closed = await service.CloseAsync(added.Value.Id);
        var update = await service.UpdateAsync(
            added.Value.Id,
            new FiscalYearUpdateRequest(
                "2026 Updated",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                true,
                closed.Value.RowVersion));

        Assert.True(closed.IsSuccess);
        Assert.Equal(FiscalYearStatus.Closed, closed.Value.Status);
        Assert.True(update.IsFailure);
        Assert.Equal(
            "FiscalYears.ClosedCannotBeModified",
            update.Error.Code);
    }

    [Fact]
    public async Task Close_BlocksWhenAccountingReadinessHasIssues()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(
            companyId: 1,
            accountingReadinessService: new BlockedReadinessService());
        var added = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));

        var close = await service.CloseAsync(added.Value.Id);

        Assert.True(close.IsFailure);
        Assert.Equal("FiscalYears.ClosingNotReady", close.Error.Code);
        Assert.Equal(
            FiscalYearStatus.Open,
            (await service.GetByIdAsync(added.Value.Id)).Value.Status);
    }

    [Fact]
    public async Task Close_TransfersFinancialPositionOnceAcrossReopen()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(
            companyId: 1,
            accountingReadinessService: new ReadyReadinessService());
        var first = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        var next = await service.AddAsync(
            new FiscalYearRequest(
                "2027",
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                IsCurrent: true));
        await database.SeedClosingLedgerAsync(
            first.Value.Id,
            next.Value.Id);
        database.ClearTracking();

        Assert.True((await service.CloseAsync(first.Value.Id)).IsSuccess);
        database.ClearTracking();
        Assert.True((await service.ReopenAsync(first.Value.Id)).IsSuccess);
        await database.ChangeClosingAssetBalanceAsync(120m);
        database.ClearTracking();
        Assert.True((await service.CloseAsync(first.Value.Id)).IsSuccess);

        var transfers = await database.LoadClosingTransfersAsync(
            first.Value.Id);
        Assert.Single(transfers);
        Assert.Equal(next.Value.Id, transfers[0].FiscalYearId);
        Assert.Equal(120m, transfers[0].Debit);
        Assert.Equal(120m, transfers[0].Credit);
    }

    [Fact]
    public async Task Reopen_ReturnsOpenYearAndDeleteBlocksCurrentYear()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var added = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));

        var closed = await service.CloseAsync(added.Value.Id);
        database.ClearTracking();
        var reopened = await service.ReopenAsync(added.Value.Id);
        var delete = await service.DeleteAsync(added.Value.Id);

        Assert.True(closed.IsSuccess);
        Assert.True(reopened.IsSuccess);
        Assert.Equal(FiscalYearStatus.Open, reopened.Value.Status);
        Assert.Null(reopened.Value.ClosedOn);
        Assert.True(delete.IsFailure);
        Assert.Equal(
            "FiscalYears.CurrentCannotBeDeleted",
            delete.Error.Code);
    }

    [Fact]
    public async Task OtherCompanyYear_IsNotVisibleOrMutable()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var otherCompany = database.CreateService(companyId: 2);
        var added = await otherCompany.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));

        var get = await service.GetByIdAsync(added.Value.Id);
        var close = await service.CloseAsync(added.Value.Id);
        var delete = await service.DeleteAsync(added.Value.Id);

        Assert.Equal("FiscalYears.NotFound", get.Error.Code);
        Assert.Equal("FiscalYears.NotFound", close.Error.Code);
        Assert.Equal("FiscalYears.NotFound", delete.Error.Code);
    }

    [Fact]
    public void UpdateValidator_RequiresEightByteRowVersion()
    {
        var validator = new FiscalYearUpdateRequestValidator();
        var result = validator.Validate(
            new FiscalYearUpdateRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                true,
                [1, 2]));

        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(FiscalYearUpdateRequest.RowVersion));
    }

    [Fact]
    public async Task PeriodGuard_AllowsOpenYearAndRejectsClosedOrUncoveredDates()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var guard = database.CreateGuard(companyId: 1);

        var uncovered = await guard.EnsureOpenAsync(
            new DateOnly(2025, 12, 31),
            "InvoiceDate");

        var added = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        var open = await guard.EnsureOpenAsync(
            new DateOnly(2026, 6, 1),
            "InvoiceDate");

        await service.CloseAsync(added.Value.Id);
        var closed = await guard.EnsureOpenAsync(
            new DateOnly(2026, 6, 1),
            "InvoiceDate");

        Assert.True(uncovered.IsFailure);
        Assert.Equal("FiscalYears.DateNotCovered", uncovered.Error.Code);
        Assert.True(added.IsSuccess);
        Assert.True(open.IsSuccess);
        Assert.True(closed.IsFailure);
        Assert.Equal("FiscalYears.Closed", closed.Error.Code);
        Assert.Equal("InvoiceDate", closed.Error.FieldName);
    }

    private sealed class BlockedReadinessService : IAccountingReadinessService
    {
        public Task<Result<AccountingReadinessResponse>> GetAsync(
            int fiscalYearId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                    Result<AccountingReadinessResponse>.Success(
                        new AccountingReadinessResponse(
                        FiscalYearId: fiscalYearId,
                        FiscalYearName: "2026",
                        StartDate: new DateOnly(2026, 1, 1),
                        EndDate: new DateOnly(2026, 12, 31),
                        IsReady: false,
                        TotalSources: 1,
                        PostedSources: 0,
                        MissingJournalSources: 1,
                        OrphanAutomaticJournals: 0,
                        DuplicateAutomaticJournals: 0,
                        UnbalancedAutomaticJournals: 0,
                        PendingInventoryCosts: 0,
                        MissingOrInvalidMappings: 0,
                        DeferredPayrollSources: 0,
                        Sources: [],
                        Issues: [new AccountingReadinessIssue(
                            IssueType: "MissingJournal",
                            SourceType: null,
                            SourceId: null,
                            SourceNumber: null,
                            SourceDate: null,
                            MappingType: null,
                            MappingSourceId: null,
                            Message: "missing")])));

        public Task<Result<AccountingBackfillResponse>> BackfillAsync(
            int fiscalYearId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ReadyReadinessService : IAccountingReadinessService
    {
        public Task<Result<AccountingReadinessResponse>> GetAsync(
            int fiscalYearId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                    Result<AccountingReadinessResponse>.Success(
                        new AccountingReadinessResponse(
                        FiscalYearId: fiscalYearId,
                        FiscalYearName: "2026",
                        StartDate: new DateOnly(2026, 1, 1),
                        EndDate: new DateOnly(2026, 12, 31),
                        IsReady: true,
                        TotalSources: 0,
                        PostedSources: 0,
                        MissingJournalSources: 0,
                        OrphanAutomaticJournals: 0,
                        DuplicateAutomaticJournals: 0,
                        UnbalancedAutomaticJournals: 0,
                        PendingInventoryCosts: 0,
                        MissingOrInvalidMappings: 0,
                        DeferredPayrollSources: 0,
                        Sources: [],
                        Issues: [])));

        public Task<Result<AccountingBackfillResponse>> BackfillAsync(
            int fiscalYearId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FiscalYearTestDatabase : IAsyncDisposable
    {
        private FiscalYearTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        private ApplicationDbContext Context { get; }

        public static async Task<FiscalYearTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var auditInterceptor = new AuditableEntityInterceptor(
                new HttpContextAccessor(),
                TimeProvider.System);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(auditInterceptor)
                .Options;
            var context = new ApplicationDbContext(options);

            await CreateSchemaAsync(context);

            return new FiscalYearTestDatabase(connection, context);
        }

        public FiscalYearService CreateService(
            int companyId,
            IAccountingReadinessService? accountingReadinessService = null) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId),
                TimeProvider.System,
                accountingReadinessService);

        public IFiscalYearPeriodGuard CreateGuard(int companyId) =>
            new FiscalYearPeriodGuard(
                Context,
                new TestCurrentCompanyContext(companyId));

        public void ClearTracking() => Context.ChangeTracker.Clear();

        public Task SeedClosingLedgerAsync(
            int fiscalYearId,
            int nextFiscalYearId) =>
            Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Accounts (
                    Id, CompanyId, Code, Name, AccountType, NormalBalance,
                    IsPosting, IsActive, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES
                    (10, 1, '1100', 'Cash', 1, 1, 1, 1, randomblob(8),
                     '', '2026-01-01', '', 0),
                    (20, 1, '4100', 'Revenue', 4, 2, 1, 1, randomblob(8),
                     '', '2026-01-01', '', 0),
                    (30, 1, '3100', 'Opening equity', 3, 2, 1, 1, randomblob(8),
                     '', '2026-01-01', '', 0);

                INSERT INTO AccountMappings (
                    CompanyId, FiscalYearId, MappingType, SourceId, AccountId,
                    RowVersion, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    1, {nextFiscalYearId}, 17, NULL, 30, randomblob(8),
                    '', '2026-01-01', '', 0);

                INSERT INTO JournalEntries (
                    Id, CompanyId, FiscalYearId, EntryNumber, EntryDate,
                    Description, EntryType, SourceType, SourceId, Status,
                    PostedOn, RowVersion, CreatedById, CreatedOn, CreatedByPc,
                    IsDeleted)
                VALUES (
                    50, 1, {fiscalYearId}, 'JV-1', '2026-12-31', 'ledger',
                    1, NULL, NULL, 1, '2026-12-31', randomblob(8), '',
                    '2026-12-31', '', 0);

                INSERT INTO JournalEntryLines (
                    Id, CompanyId, JournalEntryId, AccountId, Debit, Credit,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (501, 1, 50, 10, 100, 0, '', '2026-12-31', '', 0),
                    (502, 1, 50, 20, 0, 100, '', '2026-12-31', '', 0);
                """);

        public Task ChangeClosingAssetBalanceAsync(decimal amount) =>
            Context.Database.ExecuteSqlInterpolatedAsync($"UPDATE JournalEntryLines SET Debit = {amount} WHERE Id = 501");

        public Task<List<ClosingTransferRow>> LoadClosingTransfersAsync(
            int sourceFiscalYearId) =>
            Context.JournalEntries
                .AsNoTracking()
                .Where(entry =>
                    entry.CompanyId == 1 &&
                    entry.EntryType == JournalEntryType.Opening &&
                    entry.SourceType == JournalEntrySourceType.FiscalYearClosing &&
                    entry.SourceId == sourceFiscalYearId)
                .Select(entry => new ClosingTransferRow(
                    entry.FiscalYearId,
                    entry.Lines.Sum(line => line.Debit),
                    entry.Lines.Sum(line => line.Credit)))
                .ToListAsync();

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static Task CreateSchemaAsync(ApplicationDbContext context) =>
            context.Database.ExecuteSqlRawAsync(
                """
                PRAGMA foreign_keys = ON;

                CREATE TABLE Companies (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NULL
                );

                CREATE TABLE FiscalYears (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    StartDate TEXT NOT NULL,
                    EndDate TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    IsCurrent INTEGER NOT NULL DEFAULT 0,
                    ClosedOn TEXT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id)
                );

                CREATE UNIQUE INDEX UX_FiscalYears_Company_Name
                ON FiscalYears (CompanyId, Name)
                WHERE IsDeleted = 0;

                CREATE UNIQUE INDEX UX_FiscalYears_Company_Current
                ON FiscalYears (CompanyId, IsCurrent)
                WHERE IsCurrent = 1 AND IsDeleted = 0;

                CREATE TRIGGER AdvanceFiscalYearRowVersion
                AFTER UPDATE ON FiscalYears
                BEGIN
                    UPDATE FiscalYears
                    SET RowVersion = randomblob(8)
                    WHERE Id = NEW.Id;
                END;

                CREATE TABLE Accounts (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    ParentAccountId INTEGER NULL,
                    AccountType INTEGER NOT NULL,
                    NormalBalance INTEGER NOT NULL,
                    IsPosting INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL,
                    RowVersion BLOB NOT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE AccountMappings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    MappingType INTEGER NOT NULL,
                    SourceId INTEGER NULL,
                    AccountId INTEGER NOT NULL,
                    RowVersion BLOB NOT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE JournalEntries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    EntryNumber TEXT NOT NULL,
                    EntryDate TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    EntryType INTEGER NOT NULL,
                    SourceType INTEGER NULL,
                    SourceId INTEGER NULL,
                    SourceNumber TEXT NULL,
                    Status INTEGER NOT NULL,
                    PostedOn TEXT NOT NULL,
                    ReversedOn TEXT NULL,
                    ReversalOfEntryId INTEGER NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE JournalEntryLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    JournalEntryId INTEGER NOT NULL,
                    AccountId INTEGER NOT NULL,
                    Description TEXT NULL,
                    Debit NUMERIC NOT NULL,
                    Credit NUMERIC NOT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                INSERT INTO Companies (Id, Name)
                VALUES (1, 'Company 1'), (2, 'Company 2');
                """);

        private sealed record TestCurrentCompanyContext(int CompanyId)
            : ICurrentCompanyContext;

        public sealed record ClosingTransferRow(
            int FiscalYearId,
            decimal Debit,
            decimal Credit);
    }
}
