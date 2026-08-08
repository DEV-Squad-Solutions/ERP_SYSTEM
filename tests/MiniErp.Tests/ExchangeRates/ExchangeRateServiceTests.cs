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
    }

    [Fact]
    public async Task Resolver_WithRequestedRate_PersistsAndReturnsManualRate()
    {
        await using var database = await ExchangeRateTestDatabase.CreateAsync();
        var resolver = database.CreateResolver(1);
        var requestedDate = new DateOnly(2026, 1, 4);

        var result = await resolver.ResolveAsync(
            CurrencyCode.GBP,
            requestedDate,
            requestedRate: 60m);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ExchangeRateId);
        Assert.Equal(CurrencyCode.EGP, result.Value.BaseCurrency);
        Assert.Equal(CurrencyCode.GBP, result.Value.Currency);
        Assert.Equal(requestedDate, result.Value.RequestedDate);
        Assert.Equal(requestedDate, result.Value.RateDate);
        Assert.Equal(60m, result.Value.Rate);
        Assert.Equal(ExchangeRateSource.Manual, result.Value.Source);
        Assert.False(result.Value.IsBaseCurrency);

        var persisted = await database.Context.ExchangeRates
            .AsNoTracking()
            .SingleAsync(rate => rate.Id == result.Value.ExchangeRateId);
        Assert.Equal(1, persisted.CompanyId);
        Assert.Equal(CurrencyCode.GBP, persisted.Currency);
        Assert.Equal(requestedDate, persisted.RateDate);
        Assert.Equal(60m, persisted.Rate);
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
                rate.Rate,
                rate.Source,
                rate.Notes,
                rate.RowVersion));

        Assert.True(result.IsFailure);
        Assert.Equal("ExchangeRates.Referenced", result.Error.Code);
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
                CREATE TABLE CashVouchers (Id INTEGER PRIMARY KEY, CompanyId INTEGER NOT NULL, ExchangeRateId INTEGER NULL, IsDeleted INTEGER NOT NULL);
                CREATE TABLE PartnerOpeningBalances (Id INTEGER PRIMARY KEY, CompanyId INTEGER NOT NULL, ExchangeRateId INTEGER NULL, IsDeleted INTEGER NOT NULL);
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

            return new ExchangeRateTestDatabase(connection, context, isolationInterceptor);
        }

        public ExchangeRateService CreateService(int companyId)
        {
            var companyContext = new TestCurrentCompanyContext(companyId);
            var resolver = CreateResolver(companyContext);

            return new ExchangeRateService(
                Context,
                new PaginationService(),
                companyContext,
                TimeProvider.System,
                resolver);
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
