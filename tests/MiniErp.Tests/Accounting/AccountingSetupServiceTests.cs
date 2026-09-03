using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Features.AccountStatementMappings;
using MiniErp.Application.Features.Accounts;
using MiniErp.Application.Features.FinancialStatementLines;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.AccountingSetup;
using MiniErp.Infrastructure.Services.AccountStatementMappings;
using MiniErp.Infrastructure.Services.Accounts;
using MiniErp.Infrastructure.Services.FinancialStatementLines;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.Accounting;

public sealed class AccountingSetupServiceTests
{
    static AccountingSetupServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task DefaultSetup_CreatesCompleteIdempotentCompanyAccountingSetup()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateDefaultAccountingSetupService();
        await database.AddCashSetupSourcesAsync();

        await service.InitializeCompanyAsync(
            companyId: 1,
            effectiveDate: new DateOnly(2026, 9, 2));
        await service.InitializeCompanyAsync(
            companyId: 1,
            effectiveDate: new DateOnly(2026, 9, 2));

        var counts = await database.GetDefaultSetupCountsAsync();

        Assert.Equal(23, counts.Accounts);
        Assert.Equal(19, counts.AccountMappings);
        Assert.Equal(34, counts.StatementLines);
        Assert.Equal(33, counts.StatementMappings);
        Assert.Equal(1, counts.FiscalYears);
    }

    [Fact]
    public async Task DefaultSetup_ExtendsNewFiscalYearsAndCashSourcesWithoutDuplicates()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateDefaultAccountingSetupService();

        await service.InitializeCompanyAsync(
            companyId: 1,
            effectiveDate: new DateOnly(2026, 9, 2));
        await database.AddFutureFiscalYearAndCashSetupSourcesAsync();

        await service.EnsureFiscalYearAsync(companyId: 1, fiscalYearId: 3);
        await service.EnsureCashboxAsync(companyId: 1, cashboxId: 1);
        await service.EnsureCashMovementTypeAsync(
            companyId: 1,
            cashMovementTypeId: 1);
        await service.EnsureFiscalYearAsync(companyId: 1, fiscalYearId: 3);

        var counts = await database.GetFiscalYearSetupCountsAsync(3);

        Assert.Equal(19, counts.AccountMappings);
        Assert.Equal(34, counts.StatementLines);
        Assert.Equal(33, counts.StatementMappings);
    }

    [Fact]
    public async Task NewAccounts_GenerateCompanyScopedUniqueCodes()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateAccountService(companyId: 1);

        var first = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "حساب تلقائي أول",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));
        var second = await service.AddAsync(new AccountRequest(
            Code: "IGNORED-CODE",
            Name: "حساب تلقائي ثان",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));
        var child = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "ابن تلقائي",
            ParentAccountId: first.Value.Id,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: true));
        var child2 = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "ابن تلقائي ثان",
            ParentAccountId: second.Value.Id,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));
        var child3 = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "ابن تلقائي ثالث",
            ParentAccountId: second.Value.Id,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: true));
        var grandchild = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "حفيد تلقائي",
            ParentAccountId: child2.Value.Id,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: true));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("1000", first.Value.Code);
        Assert.Equal("2000", second.Value.Code);
        Assert.Equal("1100", child.Value.Code);
        Assert.Equal("2100", child2.Value.Code);
        Assert.Equal("2200", child3.Value.Code);
        Assert.Equal("2110", grandchild.Value.Code);
    }

    [Fact]
    public async Task NewAccount_ReusesCodeOfSoftDeletedAccount()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateAccountService(companyId: 1);

        var deletedAccount = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "حساب سيُحذف",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));
        await database.SoftDeleteAccountAsync(deletedAccount.Value.Id);
        database.ClearTracking();
        var replacementAccount = await service.AddAsync(new AccountRequest(
            Code: null,
            Name: "حساب بديل",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));

        Assert.True(replacementAccount.IsSuccess);
        Assert.Equal(deletedAccount.Value.Code, replacementAccount.Value.Code);
        Assert.Equal("1000", replacementAccount.Value.Code);
    }

    [Fact]
    public async Task Accounts_InheritParentClassificationAndPreventHierarchyCycles()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateAccountService(companyId: 1);

        var root = await service.AddAsync(new AccountRequest(
            Code: "1000",
            Name: "الأصول",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));
        var child = await service.AddAsync(new AccountRequest(
            Code: "1100",
            Name: "الأصول المتداولة",
            ParentAccountId: root.Value.Id,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));
        var inheritedClassification = await service.AddAsync(new AccountRequest(
            Code: "5100",
            Name: "حساب فرعي",
            ParentAccountId: root.Value.Id,
            AccountType: AccountType.Expense,
            NormalBalance: NormalBalance.Credit,
            IsPosting: true));
        var updatedChild = await service.UpdateAsync(
            child.Value.Id,
            new AccountUpdateRequest(
                Code: child.Value.Code,
                Name: child.Value.Name,
                ParentAccountId: root.Value.Id,
                AccountType: AccountType.Expense,
                NormalBalance: NormalBalance.Credit,
                IsPosting: child.Value.IsPosting,
                IsActive: child.Value.IsActive,
                RowVersion: child.Value.RowVersion));
        var cycle = await service.UpdateAsync(
            root.Value.Id,
            new AccountUpdateRequest(
                Code: root.Value.Code,
                Name: root.Value.Name,
                ParentAccountId: child.Value.Id,
                AccountType: root.Value.AccountType,
                NormalBalance: root.Value.NormalBalance,
                IsPosting: root.Value.IsPosting,
                IsActive: root.Value.IsActive,
                RowVersion: root.Value.RowVersion));

        Assert.True(root.IsSuccess);
        Assert.True(child.IsSuccess);
        Assert.True(inheritedClassification.IsSuccess);
        Assert.Equal(AccountType.Asset, inheritedClassification.Value.AccountType);
        Assert.Equal(NormalBalance.Debit, inheritedClassification.Value.NormalBalance);
        Assert.True(updatedChild.IsSuccess);
        Assert.Equal(AccountType.Asset, updatedChild.Value.AccountType);
        Assert.Equal(NormalBalance.Debit, updatedChild.Value.NormalBalance);
        Assert.Equal("Accounts.HierarchyCycle", cycle.Error.Code);
    }

    [Fact]
    public async Task AccountWithMovements_CannotReceiveChildAccount()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var service = database.CreateAccountService(companyId: 1);

        var parent = await service.AddAsync(new AccountRequest(
            Code: "1200",
            Name: "حساب عليه حركة",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: false));

        await database.AddCashVoucherMovementAsync(parent.Value.Id);
        database.ClearTracking();

        var result = await service.AddAsync(new AccountRequest(
            Code: "1210",
            Name: "حساب فرعي جديد",
            ParentAccountId: parent.Value.Id,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: true));

        Assert.True(result.IsFailure);
        Assert.Equal("Accounts.ParentHasMovements", result.Error.Code);
    }

    [Fact]
    public async Task MappingReplace_ReturnsAllRowErrorsAndSavesAtomically()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var accountService = database.CreateAccountService(companyId: 1);
        var lineService = database.CreateLineService(companyId: 1);
        var mappingService = database.CreateMappingService(companyId: 1);

        var account = await accountService.AddAsync(new AccountRequest(
            Code: "4100",
            Name: "إيرادات المبيعات",
            ParentAccountId: null,
            AccountType: AccountType.Revenue,
            NormalBalance: NormalBalance.Credit,
            IsPosting: true));
        var line = await lineService.AddAsync(new FinancialStatementLineRequest(
            FiscalYearId: 1,
            StatementType: FinancialStatementType.IncomeStatement,
            Code: "IS-110",
            Name: "إيرادات المبيعات",
            ParentLineId: null,
            DisplayOrder: 110,
            IsAssignable: true));

        var invalid = await mappingService.ReplaceAsync(
            fiscalYearId: 1,
            statementType: FinancialStatementType.IncomeStatement,
            request: new ReplaceAccountStatementMappingsRequest(
                Mappings:
                [
                    new AccountStatementMappingRowRequest(
                        AccountId: account.Value.Id,
                        FinancialStatementLineId: line.Value.Id),
                    new AccountStatementMappingRowRequest(
                        AccountId: account.Value.Id,
                        FinancialStatementLineId: line.Value.Id),
                    new AccountStatementMappingRowRequest(
                        AccountId: 999,
                        FinancialStatementLineId: 999)
                ]));

        Assert.True(invalid.IsFailure);
        Assert.Contains(
            invalid.Errors,
            error => error.Code == "AccountStatementMappings.DuplicateAccount");
        Assert.Contains(
            invalid.Errors,
            error => error.Code == "AccountStatementMappings.AccountNotFound");
        Assert.Contains(
            invalid.Errors,
            error => error.Code == "AccountStatementMappings.LineNotFound");
        Assert.Empty((await mappingService.GetAsync(
            1,
            FinancialStatementType.IncomeStatement)).Value);

        var saved = await mappingService.ReplaceAsync(
            fiscalYearId: 1,
            statementType: FinancialStatementType.IncomeStatement,
            request: new ReplaceAccountStatementMappingsRequest(
                Mappings:
                [
                    new AccountStatementMappingRowRequest(
                        AccountId: account.Value.Id,
                        FinancialStatementLineId: line.Value.Id)
                ]));

        Assert.True(saved.IsSuccess);
        Assert.Single(saved.Value);
        Assert.Equal(account.Value.Id, saved.Value[0].AccountId);
        Assert.Equal(line.Value.Id, saved.Value[0].FinancialStatementLineId);
    }

    [Fact]
    public async Task ClosedFiscalYear_BlocksLineAndMappingChangesButAllowsReading()
    {
        await using var database = await AccountingTestDatabase.CreateAsync();
        var accountService = database.CreateAccountService(companyId: 1);
        var lineService = database.CreateLineService(companyId: 1);
        var mappingService = database.CreateMappingService(companyId: 1);

        var account = await accountService.AddAsync(new AccountRequest(
            Code: "1200",
            Name: "العملاء",
            ParentAccountId: null,
            AccountType: AccountType.Asset,
            NormalBalance: NormalBalance.Debit,
            IsPosting: true));
        var line = await lineService.AddAsync(new FinancialStatementLineRequest(
            FiscalYearId: 1,
            StatementType: FinancialStatementType.FinancialPosition,
            Code: "FP-120",
            Name: "العملاء",
            ParentLineId: null,
            DisplayOrder: 120,
            IsAssignable: true));
        var saved = await mappingService.ReplaceAsync(
            fiscalYearId: 1,
            statementType: FinancialStatementType.FinancialPosition,
            request: new ReplaceAccountStatementMappingsRequest(
                Mappings:
                [
                    new AccountStatementMappingRowRequest(
                        AccountId: account.Value.Id,
                        FinancialStatementLineId: line.Value.Id)
                ]));

        await database.CloseFiscalYearAsync(1);
        database.ClearTracking();

        var addLine = await lineService.AddAsync(new FinancialStatementLineRequest(
            FiscalYearId: 1,
            StatementType: FinancialStatementType.FinancialPosition,
            Code: "FP-130",
            Name: "المخزون",
            ParentLineId: null,
            DisplayOrder: 130,
            IsAssignable: true));
        var replace = await mappingService.ReplaceAsync(
            fiscalYearId: 1,
            statementType: FinancialStatementType.FinancialPosition,
            request: new ReplaceAccountStatementMappingsRequest(Mappings: []));
        var read = await mappingService.GetAsync(
            1,
            FinancialStatementType.FinancialPosition);

        Assert.True(saved.IsSuccess);
        Assert.Equal("FinancialStatementLines.FiscalYearClosed", addLine.Error.Code);
        Assert.Equal("AccountStatementMappings.FiscalYearClosed", replace.Error.Code);
        Assert.True(read.IsSuccess);
        Assert.Single(read.Value);
    }

    private sealed class AccountingTestDatabase : IAsyncDisposable
    {
        private AccountingTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        private ApplicationDbContext Context { get; }

        public static async Task<AccountingTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(new AuditableEntityInterceptor(
                    new HttpContextAccessor(),
                    TimeProvider.System))
                .Options;
            var context = new ApplicationDbContext(options);
            await CreateSchemaAsync(context);
            return new AccountingTestDatabase(connection, context);
        }

        public AccountService CreateAccountService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public FinancialStatementLineService CreateLineService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public AccountStatementMappingService CreateMappingService(int companyId) =>
            new(Context, new TestCurrentCompanyContext(companyId));

        public DefaultAccountingSetupService
            CreateDefaultAccountingSetupService() => new(Context);

        public Task AddCashSetupSourcesAsync() =>
            Context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Cashboxes (Id, CompanyId, IsDeleted)
                VALUES (1, 1, 0);

                INSERT INTO CashMovementTypes (
                    Id, CompanyId, Direction, Classification, IsDeleted)
                VALUES (1, 1, 2, 2, 0);
                """);

        public Task AddFutureFiscalYearAndCashSetupSourcesAsync() =>
            Context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO FiscalYears (
                    Id, CompanyId, Name, StartDate, EndDate, Status, IsCurrent,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    3, 1, '2027', '2027-01-01', '2027-12-31', 1, 0,
                    'test', CURRENT_TIMESTAMP, 'test', 0);

                INSERT INTO Cashboxes (Id, CompanyId, IsDeleted)
                VALUES (1, 1, 0);

                INSERT INTO CashMovementTypes (
                    Id, CompanyId, Direction, Classification, IsDeleted)
                VALUES (1, 1, 2, 2, 0);
                """);

        public async Task<(int Accounts, int AccountMappings,
            int StatementLines, int StatementMappings, int FiscalYears)>
            GetDefaultSetupCountsAsync() =>
            (
                await Context.Accounts.CountAsync(
                    account => account.CompanyId == 1),
                await Context.AccountMappings.CountAsync(
                    mapping => mapping.CompanyId == 1),
                await Context.FinancialStatementLines.CountAsync(
                    line => line.CompanyId == 1),
                await Context.AccountStatementMappings.CountAsync(
                    mapping => mapping.CompanyId == 1),
                await Context.FiscalYears.CountAsync(
                    year => year.CompanyId == 1)
            );

        public async Task<(int AccountMappings, int StatementLines,
            int StatementMappings)> GetFiscalYearSetupCountsAsync(
            int fiscalYearId) =>
            (
                await Context.AccountMappings.CountAsync(mapping =>
                    mapping.CompanyId == 1 &&
                    mapping.FiscalYearId == fiscalYearId),
                await Context.FinancialStatementLines.CountAsync(line =>
                    line.CompanyId == 1 &&
                    line.FiscalYearId == fiscalYearId),
                await Context.AccountStatementMappings.CountAsync(mapping =>
                    mapping.CompanyId == 1 &&
                    mapping.FiscalYearId == fiscalYearId)
            );

        public Task CloseFiscalYearAsync(int fiscalYearId) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE FiscalYears SET Status = 2 WHERE Id = {fiscalYearId}");

        public void ClearTracking() => Context.ChangeTracker.Clear();

        public Task AddCashVoucherMovementAsync(int accountId) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CashVouchers (CompanyId, AccountId, IsDeleted) VALUES (1, {accountId}, 0)");

        public Task SoftDeleteAccountAsync(int accountId) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Accounts SET IsDeleted = 1 WHERE Id = {accountId}");

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
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE Accounts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    ParentAccountId INTEGER NULL,
                    AccountType INTEGER NOT NULL,
                    NormalBalance INTEGER NOT NULL,
                    IsPosting INTEGER NOT NULL DEFAULT 0,
                    IsActive INTEGER NOT NULL DEFAULT 1,
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
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id),
                    FOREIGN KEY (ParentAccountId) REFERENCES Accounts (Id)
                );

                CREATE UNIQUE INDEX UX_Accounts_Company_Code
                ON Accounts (CompanyId, Code) WHERE IsDeleted = 0;

                CREATE TABLE AccountMappings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    MappingType INTEGER NOT NULL,
                    SourceId INTEGER NULL,
                    AccountId INTEGER NOT NULL,
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
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id),
                    FOREIGN KEY (FiscalYearId) REFERENCES FiscalYears (Id),
                    FOREIGN KEY (AccountId) REFERENCES Accounts (Id)
                );

                CREATE UNIQUE INDEX UX_AccountMappings_Scope_Type_Source
                ON AccountMappings (
                    CompanyId, FiscalYearId, MappingType, SourceId)
                WHERE IsDeleted = 0;

                CREATE TABLE Cashboxes (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE CashMovementTypes (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    Direction INTEGER NOT NULL,
                    Classification INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE CashVouchers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    AccountId INTEGER NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE JournalEntryLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    AccountId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE FinancialStatementLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    StatementType INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    ParentLineId INTEGER NULL,
                    DisplayOrder INTEGER NOT NULL,
                    IsAssignable INTEGER NOT NULL DEFAULT 0,
                    IsActive INTEGER NOT NULL DEFAULT 1,
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
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id),
                    FOREIGN KEY (FiscalYearId) REFERENCES FiscalYears (Id),
                    FOREIGN KEY (ParentLineId) REFERENCES FinancialStatementLines (Id)
                );

                CREATE UNIQUE INDEX UX_FinancialStatementLines_Scope_Code
                ON FinancialStatementLines (CompanyId, FiscalYearId, StatementType, Code)
                WHERE IsDeleted = 0;

                CREATE TABLE AccountStatementMappings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    StatementType INTEGER NOT NULL,
                    AccountId INTEGER NOT NULL,
                    FinancialStatementLineId INTEGER NOT NULL,
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
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id),
                    FOREIGN KEY (FiscalYearId) REFERENCES FiscalYears (Id),
                    FOREIGN KEY (AccountId) REFERENCES Accounts (Id),
                    FOREIGN KEY (FinancialStatementLineId) REFERENCES FinancialStatementLines (Id)
                );

                CREATE UNIQUE INDEX UX_AccountStatementMappings_Scope_Account
                ON AccountStatementMappings (CompanyId, FiscalYearId, StatementType, AccountId)
                WHERE IsDeleted = 0;

                CREATE TRIGGER AdvanceAccountRowVersion
                AFTER UPDATE ON Accounts
                BEGIN
                    UPDATE Accounts SET RowVersion = randomblob(8) WHERE Id = NEW.Id;
                END;

                CREATE TRIGGER AdvanceStatementLineRowVersion
                AFTER UPDATE ON FinancialStatementLines
                BEGIN
                    UPDATE FinancialStatementLines
                    SET RowVersion = randomblob(8) WHERE Id = NEW.Id;
                END;

                INSERT INTO Companies (Id, Name)
                VALUES (1, 'Company 1'), (2, 'Company 2');

                INSERT INTO FiscalYears (
                    Id, CompanyId, Name, StartDate, EndDate, Status, IsCurrent,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    1, 1, '2026', '2026-01-01', '2026-12-31', 1, 1,
                    'test', CURRENT_TIMESTAMP, 'test', 0),
                    (2, 2, '2026', '2026-01-01', '2026-12-31', 1, 1,
                    'test', CURRENT_TIMESTAMP, 'test', 0);
                """);

        private sealed record TestCurrentCompanyContext(int CompanyId)
            : ICurrentCompanyContext;
    }
}
