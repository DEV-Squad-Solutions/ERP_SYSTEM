using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.ExchangeRates;
using MiniErp.Infrastructure.Services.FiscalYears;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.ExchangeRates;

public sealed class ExchangeRateServiceTests
{
    static ExchangeRateServiceTests()
    {
        MappingConfiguration.Register(typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task GetAll_SearchMatchesCurrencyCodeAndNotes()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        var service = database.CreateService(1);

        var currencyResult = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new ExchangeRateFilterRequest(Search: " usd "));
        var notesResult = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new ExchangeRateFilterRequest(Search: " month end "));

        Assert.True(currencyResult.IsSuccess);
        Assert.Single(currencyResult.Value.Items);
        Assert.Equal(CurrencyCode.USD, currencyResult.Value.Items[0].Currency);
        Assert.True(notesResult.IsSuccess);
        Assert.Single(notesResult.Value.Items);
        Assert.Equal("Month end", notesResult.Value.Items[0].Notes);
    }

    [Fact]
    public async Task GetAll_SearchIsTenantScoped()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        var companyOne = database.CreateService(1);
        var companyTwo = database.CreateService(2);

        var companyOneResult = await companyOne.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new ExchangeRateFilterRequest(Search: "tenant two"));
        var companyTwoResult = await companyTwo.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new ExchangeRateFilterRequest(Search: "tenant two"));

        Assert.True(companyOneResult.IsSuccess);
        Assert.Empty(companyOneResult.Value.Items);
        Assert.True(companyTwoResult.IsSuccess);
        Assert.Single(companyTwoResult.Value.Items);
    }

    [Fact]
    public async Task GetAll_WithoutFiscalYearFilter_ReturnsOnlyCurrentFiscalYear()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE FiscalYears
            SET Name = '2025', StartDate = '2025-01-01', EndDate = '2025-12-31'
            WHERE Id = 1;

            INSERT INTO FiscalYears (
                Id, CompanyId, Name, StartDate, EndDate, Status,
                IsCurrent, RowVersion, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES (
                100, 1, '2026', '2026-01-01', '2026-12-31', 1,
                0, randomblob(8), 'test', '2026-01-01', 'test', 0);

            UPDATE ExchangeRates
            SET FiscalYearId = 100
            WHERE CompanyId = 1;

            INSERT INTO ExchangeRates (
                CompanyId, FiscalYearId, Currency, RateDate, Rate, Source,
                Notes, LastModifiedAt, CreatedById, CreatedOn, CreatedByPc,
                IsDeleted)
            VALUES (
                1, 1, 4, '2025-12-31', 60, 1,
                'Current fiscal year', '2025-12-31', 'test',
                '2025-12-31', 'test', 0);
            """);
        var service = database.CreateService(1);

        var currentYearResult = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 });
        var explicitNextYearResult = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new ExchangeRateFilterRequest(FiscalYearId: 100));

        Assert.True(currentYearResult.IsSuccess);
        var currentRate = Assert.Single(currentYearResult.Value.Items);
        Assert.Equal(1, currentRate.FiscalYearId);
        Assert.Equal(new DateOnly(2025, 12, 31), currentRate.RateDate);

        Assert.True(explicitNextYearResult.IsSuccess);
        Assert.Equal(2, explicitNextYearResult.Value.Items.Count);
        Assert.All(explicitNextYearResult.Value.Items, rate =>
            Assert.Equal(100, rate.FiscalYearId));

        var nextYearRateId = explicitNextYearResult.Value.Items[0].Id;
        var hiddenNextYearRate = await service.GetByIdAsync(nextYearRateId);
        var explicitNextYearRate = await service.GetByIdAsync(
            nextYearRateId,
            fiscalYearId: 100);
        var currentYearRate = await service.GetByIdAsync(currentRate.Id);

        Assert.True(hiddenNextYearRate.IsFailure);
        Assert.Equal("ExchangeRates.NotFound", hiddenNextYearRate.Error.Code);
        Assert.True(explicitNextYearRate.IsSuccess);
        Assert.Equal(100, explicitNextYearRate.Value.FiscalYearId);
        Assert.True(currentYearRate.IsSuccess);
        Assert.Equal(1, currentYearRate.Value.FiscalYearId);

        var crossYearAdd = await service.AddAsync(
            new ExchangeRateRequest(
                Currency: CurrencyCode.SAR,
                RateDate: new DateOnly(2026, 6, 1),
                Rate: 13m));
        var nextRate = explicitNextYearRate.Value;
        var crossYearUpdate = await service.UpdateAsync(
            nextRate.Id,
            new ExchangeRateUpdateRequest(
                Currency: nextRate.Currency,
                RateDate: nextRate.RateDate,
                Rate: nextRate.Rate + 1m,
                Source: ExchangeRateSource.Manual,
                Notes: nextRate.Notes,
                RowVersion: nextRate.RowVersion));
        var crossYearDelete = await service.DeleteAsync(nextRate.Id);

        Assert.Equal(
            "FiscalYears.QueryDateOutsideRange",
            crossYearAdd.Error.Code);
        Assert.Equal(
            "FiscalYears.QueryDateOutsideRange",
            crossYearUpdate.Error.Code);
        Assert.Equal(
            "FiscalYears.QueryDateOutsideRange",
            crossYearDelete.Error.Code);
    }

    [Fact]
    public async Task GetAll_ReturnsNewestRateDateFirst_WithDescendingIdTieBreak()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ExchangeRates
                (Id, CompanyId, Currency, RateDate, Rate, Source, Notes,
                 LastModifiedAt, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (4, 1, 4, '2026-01-02', 60, 1, 'Same-date tie',
                 '2026-01-02', 'test', '2026-01-02', 'test', 0);
            """);
        var service = database.CreateService(1);

        var result = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 });

        Assert.True(result.IsSuccess);
        Assert.Equal([4, 2, 1], result.Value.Items.Select(rate => rate.Id));
    }

    [Fact]
    public async Task Resolve_UsesDedicatedResolverAndMapsEveryField()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        var service = database.CreateService(1);
        var requestedDate = new DateOnly(2026, 1, 3);

        var result = await service.ResolveAsync(
            CurrencyCode.USD,
            requestedDate);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.ExchangeRateId);
        Assert.Equal(CurrencyCode.EGP, result.Value.BaseCurrency);
        Assert.Equal(CurrencyCode.USD, result.Value.Currency);
        Assert.Equal(requestedDate, result.Value.RequestedDate);
        Assert.Equal(new DateOnly(2026, 1, 1), result.Value.RateDate);
        Assert.Equal(50m, result.Value.Rate);
        Assert.Equal(ExchangeRateSource.Manual, result.Value.Source);
        Assert.False(result.Value.IsBaseCurrency);

        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE FiscalYears
            SET IsCurrent = 2,
                Name = '2025', StartDate = '2025-01-01', EndDate = '2025-12-31'
            WHERE Id = 1;
            INSERT INTO FiscalYears (
                Id, CompanyId, Name, StartDate, EndDate, Status,
                IsCurrent, RowVersion, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES (
                100, 1, '2026', '2026-01-01', '2026-12-31', 1,
                1, randomblob(8), 'test', '2026-01-01', 'test', 0);
            UPDATE FiscalYears SET IsCurrent = 0 WHERE Id = 1;
            UPDATE ExchangeRates SET FiscalYearId = 100 WHERE CompanyId = 1;
            INSERT INTO ExchangeRates (
                CompanyId, FiscalYearId, Currency, RateDate, Rate, Source,
                Notes, LastModifiedAt, CreatedById, CreatedOn, CreatedByPc,
                IsDeleted)
            VALUES (
                1, 1, 4, '2025-01-01', 49, 1, 'Historical',
                '2025-01-01', 'test', '2025-01-01', 'test', 0);
            """);

        var hiddenHistoricalResolve = await service.ResolveAsync(
            CurrencyCode.USD,
            new DateOnly(2025, 1, 3));
        var selectedHistoricalResolve = await service.ResolveAsync(
            CurrencyCode.USD,
            new DateOnly(2025, 1, 3),
            fiscalYearId: 1);

        Assert.True(hiddenHistoricalResolve.IsFailure);
        Assert.Equal(
            "FiscalYears.QueryDateOutsideRange",
            hiddenHistoricalResolve.Error.Code);
        Assert.True(selectedHistoricalResolve.IsFailure);
    }

    [Fact]
    public async Task Resolver_WithRequestedRate_ReturnsManualSnapshotWithoutPersisting()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        var resolver = database.CreateResolver(1);
        var requestedDate = new DateOnly(2026, 1, 4);

        var result = await resolver.ResolveAsync(
            CurrencyCode.GBP,
            requestedDate,
            requestedRate: 60m);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ExchangeRateId);
        Assert.Equal(CurrencyCode.EGP, result.Value.BaseCurrency);
        Assert.Equal(CurrencyCode.GBP, result.Value.Currency);
        Assert.Equal(requestedDate, result.Value.RequestedDate);
        Assert.Equal(requestedDate, result.Value.RateDate);
        Assert.Equal(60m, result.Value.Rate);
        Assert.Equal(ExchangeRateSource.Manual, result.Value.Source);
        Assert.False(result.Value.IsBaseCurrency);

        Assert.False(await database.Context.ExchangeRates.AnyAsync(rate =>
            rate.CompanyId == 1 &&
            rate.Currency == CurrencyCode.GBP &&
            rate.RateDate == requestedDate));
    }

    [Fact]
    public async Task Resolver_WithRequestedRate_DoesNotReuseOrMutateExistingDailyRate()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        var requestedDate = new DateOnly(2026, 1, 4);
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ExchangeRates
                (CompanyId, Currency, RateDate, Rate, Source, Notes,
                 LastModifiedAt, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 4, '2026-01-04', 55, 1, 'Global daily rate',
                 '2026-01-04', 'test', '2026-01-04', 'test', 0);
            """);
        var resolver = database.CreateResolver(1);

        var result = await resolver.ResolveAsync(
            CurrencyCode.GBP,
            requestedDate,
            requestedRate: 60m);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ExchangeRateId);
        Assert.Equal(60m, result.Value.Rate);
        Assert.Equal(requestedDate, result.Value.RateDate);
        Assert.Equal(ExchangeRateSource.Manual, result.Value.Source);

        var persisted = await database.Context.ExchangeRates
            .AsNoTracking()
            .SingleAsync(rate =>
                rate.CompanyId == 1 &&
                rate.Currency == CurrencyCode.GBP &&
                rate.RateDate == requestedDate);
        Assert.Equal(55m, persisted.Rate);
        Assert.Equal(ExchangeRateSource.Manual, persisted.Source);
    }

    [Fact]
    public async Task Add_WhenUniqueIndexWinsRace_ReturnsDuplicateConflict()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync(
            addDuplicateRaceTrigger: true);
        var service = database.CreateService(1);

        var result = await service.AddAsync(
            new ExchangeRateRequest(
                CurrencyCode.GBP,
                new DateOnly(2026, 1, 5),
                10m,
                Notes: "race"));

        Assert.True(result.IsFailure);
        Assert.Equal("ExchangeRates.Duplicate", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAndDelete_StartSerializableTransactionsForReferenceChecks()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        var service = database.CreateService(1);
        var rate = await database.GetRateAsync(1);

        var update = await service.UpdateAsync(
            1,
            new ExchangeRateUpdateRequest(
                rate.Currency,
                rate.RateDate,
                rate.Rate,
                rate.Source,
                rate.Notes,
                rate.RowVersion));
        Assert.True(update.IsSuccess);

        var delete = await service.DeleteAsync(1);
        Assert.True(delete.IsSuccess);
        Assert.All(database.IsolationLevels, isolation =>
            Assert.Equal(System.Data.IsolationLevel.Serializable, isolation));
        Assert.Equal(2, database.IsolationLevels.Count);
    }

    [Fact]
    public async Task Import_OnHolidayStoresProviderRateOnRequestedDateInTheOpenYear()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        // 2025 is closed; 1 January 2026 is a holiday, so the provider
        // answers with its 31 December 2025 publication.
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE FiscalYears
            SET Name = '2025', StartDate = '2025-01-01',
                EndDate = '2025-12-31', Status = 2, IsCurrent = 0
            WHERE Id = 1;

            INSERT INTO FiscalYears (
                Id, CompanyId, Name, StartDate, EndDate, Status,
                IsCurrent, RowVersion, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES (
                100, 1, '2026', '2026-01-01', '2026-12-31', 1,
                1, randomblob(8), 'test', '2026-01-01', 'test', 0);
            """);
        var service = database.CreateService(
            1,
            new FixedDateExchangeRateProvider(
                publishedDate: new DateOnly(2025, 12, 31),
                rate: 62.5m));

        var result = await service.ImportAsync(
            new ExchangeRateImportRequest(
                RateDate: new DateOnly(2026, 1, 1),
                Currencies: [CurrencyCode.GBP]));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(1, result.Value.ImportedCount);
        var stored = await database.Context.ExchangeRates
            .AsNoTracking()
            .SingleAsync(rate =>
                rate.CompanyId == 1 &&
                rate.Currency == CurrencyCode.GBP);
        Assert.Equal(new DateOnly(2026, 1, 1), stored.RateDate);
        Assert.Equal(100, stored.FiscalYearId);
        Assert.Equal(62.5m, stored.Rate);
        Assert.Equal(ExchangeRateSource.Imported, stored.Source);
        Assert.Contains("2025-12-31", stored.Notes);

        var resolved = await database.CreateResolver(1).ResolveAsync(
            CurrencyCode.GBP,
            new DateOnly(2026, 1, 1));
        Assert.True(resolved.IsSuccess, resolved.Error.Description);
        Assert.Equal(62.5m, resolved.Value.Rate);
    }

    [Fact]
    public async Task Update_WhenReferencedByAnInvoiceReturnsConflict()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        await database.AddReferenceAsync("Invoices", 1);
        var service = database.CreateService(1);
        var rate = await database.GetRateAsync(1);

        var result = await service.UpdateAsync(
            1,
            new ExchangeRateUpdateRequest(
                rate.Currency,
                rate.RateDate,
                rate.Rate + 1m,
                rate.Source,
                rate.Notes,
                rate.RowVersion));

        Assert.True(result.IsFailure);
        Assert.Equal("ExchangeRates.Referenced", result.Error.Code);
    }

    [Fact]
    public async Task Update_NotesOnReferencedRate_DoesNotNeedLinkedUpdateAndKeepsSource()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        await database.AddReferenceAsync("Invoices", 1);
        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE ExchangeRates SET Source = 2 WHERE Id = 1");
        var service = database.CreateService(1);
        var rate = await database.GetRateAsync(1);

        var result = await service.UpdateAsync(
            1,
            new ExchangeRateUpdateRequest(
                rate.Currency,
                rate.RateDate,
                rate.Rate,
                ExchangeRateSource.Manual,
                "  ملاحظة جديدة  ",
                rate.RowVersion));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal("ملاحظة جديدة", result.Value.Notes);
        Assert.Equal(rate.Rate, result.Value.Rate);
        Assert.Equal(ExchangeRateSource.Imported, result.Value.Source);
    }

    [Fact]
    public async Task Delete_WhenReferencedByACashVoucherReturnsConflict()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        await database.AddReferenceAsync("CashVouchers", 1);
        var service = database.CreateService(1);

        var result = await service.DeleteAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal("ExchangeRates.Referenced", result.Error.Code);
    }

    [Fact]
    public async Task FilterValidator_TrimsSearchBeforeLengthValidation()
    {
        var validator = new ExchangeRateFilterRequestValidator();

        var valid = await validator.ValidateAsync(
            new ExchangeRateFilterRequest(Search: $"  {new string('x', 500)}  "));
        var invalid = await validator.ValidateAsync(
            new ExchangeRateFilterRequest(Search: new string('x', 501)));

        Assert.True(valid.IsValid);
        Assert.False(invalid.IsValid);
    }

    private sealed class ExchangeRateTestDatabase : IAsyncDisposable
    {
        private ExchangeRateTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context,
            IsolationCaptureInterceptor isolationInterceptor)
        {
            Connection = connection;
            Context = context;
            IsolationInterceptor = isolationInterceptor;
        }

        private SqliteConnection Connection { get; }

        public ApplicationDbContext Context { get; }

        private IsolationCaptureInterceptor IsolationInterceptor { get; }

        public IReadOnlyList<System.Data.IsolationLevel> IsolationLevels =>
            IsolationInterceptor.Levels;

        public static async Task<ExchangeRateTestDatabase> CreateAsync(
            bool addDuplicateRaceTrigger = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var isolationInterceptor = new IsolationCaptureInterceptor();
            var auditInterceptor = new AuditableEntityInterceptor(
                new HttpContextAccessor(),
                TimeProvider.System);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(auditInterceptor, isolationInterceptor)
                .Options;
            var context = new ApplicationDbContext(options);

            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE Companies (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Address TEXT NOT NULL,
                    CommercialRegister TEXT NOT NULL,
                    TaxNumber TEXT NOT NULL,
                    ManagerName TEXT NOT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8))
                );
                CREATE TABLE CompanySettings (
                    CompanyId INTEGER NOT NULL PRIMARY KEY,
                    BaseCurrency INTEGER NOT NULL DEFAULT 1,
                    StockBalanceCheckMode INTEGER NOT NULL DEFAULT 1
                );
                CREATE TABLE ExchangeRates (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Currency INTEGER NOT NULL,
                    RateDate TEXT NOT NULL,
                    Rate NUMERIC NOT NULL,
                    Source INTEGER NOT NULL,
                    Provider TEXT NULL,
                    Notes TEXT NULL,
                    LastModifiedAt TEXT NOT NULL,
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
                    IsDeleted INTEGER NOT NULL
                );
                CREATE UNIQUE INDEX UX_ExchangeRates_Company_Currency_Date
                    ON ExchangeRates (CompanyId, Currency, RateDate)
                    WHERE IsDeleted = 0;
                CREATE TABLE Invoices (Id INTEGER PRIMARY KEY, CompanyId INTEGER NOT NULL, ExchangeRateId INTEGER NULL, IsDeleted INTEGER NOT NULL);
                CREATE TABLE CashVouchers (Id INTEGER PRIMARY KEY, CompanyId INTEGER NOT NULL, EmployeeId INTEGER NULL, AccountId INTEGER NULL, Classification INTEGER NULL, ExchangeRateId INTEGER NULL, CashboxTransferId INTEGER NULL, IsDeleted INTEGER NOT NULL);
                CREATE TABLE PartnerOpeningBalances (Id INTEGER PRIMARY KEY, CompanyId INTEGER NOT NULL, ExchangeRateId INTEGER NULL, IsDeleted INTEGER NOT NULL);
                CREATE TABLE EmployeeOpeningBalances (Id INTEGER PRIMARY KEY, CompanyId INTEGER NOT NULL, ExchangeRateId INTEGER NULL, IsDeleted INTEGER NOT NULL);
                CREATE TABLE Cashboxes (Id INTEGER PRIMARY KEY, CompanyId INTEGER NOT NULL, OpeningExchangeRateId INTEGER NULL, IsDeleted INTEGER NOT NULL);
                INSERT INTO Companies (Id, Name, Address, CommercialRegister, TaxNumber, ManagerName, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (1, 'Company A', '', 'CR-A', 'TX-A', 'Manager', 'test', '2026-01-01', 'test', 0),
                       (2, 'Company B', '', 'CR-B', 'TX-B', 'Manager', 'test', '2026-01-01', 'test', 0);
                INSERT INTO CompanySettings (CompanyId, BaseCurrency, StockBalanceCheckMode)
                VALUES (1, 1, 1), (2, 1, 1);
                INSERT INTO ExchangeRates (Id, CompanyId, Currency, RateDate, Rate, Source, Notes, LastModifiedAt, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (1, 1, 2, '2026-01-01', 50, 1, 'Reference rate', '2026-01-01', 'test', '2026-01-01', 'test', 0),
                       (2, 1, 3, '2026-01-02', 55, 1, 'Month end', '2026-01-01', 'test', '2026-01-01', 'test', 0),
                       (3, 2, 2, '2026-01-01', 50, 1, 'Tenant two', '2026-01-01', 'test', '2026-01-01', 'test', 0);
                """);

            if (addDuplicateRaceTrigger)
            {
                await context.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TRIGGER ForceExchangeRateDuplicate
                    BEFORE INSERT ON ExchangeRates
                    WHEN NEW.Notes = 'race'
                    BEGIN
                        SELECT RAISE(ABORT, 'UNIQUE constraint failed: ExchangeRates.CompanyId, ExchangeRates.Currency, ExchangeRates.RateDate');
                    END;
                    """);
            }

            await TestFiscalYearSchema.EnsureAsync(context);

            return new ExchangeRateTestDatabase(connection, context, isolationInterceptor);
        }

        public ExchangeRateService CreateService(
            int companyId,
            IExchangeRateProvider? exchangeRateProvider = null)
        {
            var companyContext = new TestCurrentCompanyContext(companyId);
            var resolver = CreateResolver(companyContext);

            return new ExchangeRateService(
                Context,
                new PaginationService(),
                companyContext,
                TimeProvider.System,
                resolver,
                new FiscalYearQueryScopeResolver(
                    Context,
                    companyContext),
                new FiscalYearPeriodGuard(
                    Context,
                    companyContext),
                exchangeRateProvider);
        }

        public ExchangeRateResolver CreateResolver(int companyId) =>
            CreateResolver(new TestCurrentCompanyContext(companyId));

        private ExchangeRateResolver CreateResolver(
            ICurrentCompanyContext companyContext) =>
            new(
                Context,
                companyContext,
                TimeProvider.System);

        public async Task<ExchangeRateRow> GetRateAsync(int id)
        {
            var row = await Context.ExchangeRates
                .AsNoTracking()
                .Where(rate => rate.Id == id)
                .Select(rate => new ExchangeRateRow(
                    rate.Currency,
                    rate.RateDate,
                    rate.Rate,
                    rate.Source,
                    rate.Notes,
                    rate.RowVersion))
                .SingleAsync();
            return row;
        }

        public Task AddReferenceAsync(string tableName, int rateId) =>
            Context.Database.ExecuteSqlRawAsync(
                tableName == "Cashboxes"
                    ? $"INSERT INTO [{tableName}] (Id, CompanyId, OpeningExchangeRateId, IsDeleted) VALUES (700, 1, {rateId}, 0)"
                    : $"INSERT INTO [{tableName}] (Id, CompanyId, ExchangeRateId, IsDeleted) VALUES (700, 1, {rateId}, 0)");

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    /// <summary>Returns a fixed rate published on <paramref name="publishedDate"/>.</summary>
    private sealed class FixedDateExchangeRateProvider(
        DateOnly publishedDate,
        decimal rate) : IExchangeRateProvider
    {
        public string Name => "Test:Provider";

        public Task<Result<ExternalExchangeRate>> GetRateAsync(
            CurrencyCode currency,
            CurrencyCode baseCurrency,
            DateOnly requestedDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ExternalExchangeRate>.Success(
                new ExternalExchangeRate(
                    Currency: currency,
                    BaseCurrency: baseCurrency,
                    RequestedDate: requestedDate,
                    RateDate: publishedDate,
                    Rate: rate,
                    Provider: Name)));
    }

    private sealed record ExchangeRateRow(
        CurrencyCode Currency,
        DateOnly RateDate,
        decimal Rate,
        ExchangeRateSource Source,
        string? Notes,
        byte[] RowVersion);

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;

    private sealed class IsolationCaptureInterceptor : DbTransactionInterceptor
    {
        public List<System.Data.IsolationLevel> Levels { get; } = [];

        public override DbTransaction TransactionStarted(
            DbConnection connection,
            TransactionEndEventData eventData,
            DbTransaction result)
        {
            Levels.Add(result.IsolationLevel);
            return base.TransactionStarted(connection, eventData, result);
        }

        public override ValueTask<DbTransaction> TransactionStartedAsync(
            DbConnection connection,
            TransactionEndEventData eventData,
            DbTransaction result,
            CancellationToken cancellationToken = default)
        {
            Levels.Add(result.IsolationLevel);
            return base.TransactionStartedAsync(
                connection,
                eventData,
                result,
                cancellationToken);
        }
    }
}
