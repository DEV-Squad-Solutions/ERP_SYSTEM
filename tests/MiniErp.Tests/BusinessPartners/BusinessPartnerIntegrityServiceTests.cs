using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Features.BusinessPartners;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.BusinessPartners;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.BusinessPartners;

public sealed class BusinessPartnerIntegrityServiceTests
{
    static BusinessPartnerIntegrityServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_WhenInvoiceExists_RejectsActiveAndHistoricalReferences(
        bool isDeleted)
    {
        await using var database =
            await BusinessPartnerIntegrityTestDatabase.CreateAsync();
        await database.AddInvoiceAsync(isDeleted);
        var service = database.CreateService();

        var result = await service.DeleteAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "BusinessPartners.HasFinancialRecords",
            result.Error.Code);

        var partner = await database.GetPartnerAsync();
        Assert.True(partner.IsActive);
        Assert.False(partner.IsDeleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_WhenOpeningBalanceExists_RejectsActiveAndHistoricalReferences(
        bool isDeleted)
    {
        await using var database =
            await BusinessPartnerIntegrityTestDatabase.CreateAsync();
        await database.AddOpeningBalanceAsync(isDeleted);
        var service = database.CreateService();

        var result = await service.DeleteAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "BusinessPartners.HasFinancialRecords",
            result.Error.Code);

        var partner = await database.GetPartnerAsync();
        Assert.True(partner.IsActive);
        Assert.False(partner.IsDeleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_WhenInvoiceExists_RejectsCurrencyChange(
        bool isDeleted)
    {
        await using var database =
            await BusinessPartnerIntegrityTestDatabase.CreateAsync();
        await database.AddInvoiceAsync(isDeleted);
        var service = database.CreateService();

        var result = await service.UpdateAsync(
            1,
            CreateRequest(CurrencyCode.USD));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "BusinessPartners.CurrencyChangeNotAllowed",
            result.Error.Code);

        var partner = await database.GetPartnerAsync();
        Assert.Equal(CurrencyCode.EGP, partner.Currency);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_WhenOpeningBalanceExists_RejectsCurrencyChange(
        bool isDeleted)
    {
        await using var database =
            await BusinessPartnerIntegrityTestDatabase.CreateAsync();
        await database.AddOpeningBalanceAsync(isDeleted);
        var service = database.CreateService();

        var result = await service.UpdateAsync(
            1,
            CreateRequest(CurrencyCode.USD));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "BusinessPartners.CurrencyChangeNotAllowed",
            result.Error.Code);

        var partner = await database.GetPartnerAsync();
        Assert.Equal(CurrencyCode.EGP, partner.Currency);
    }

    [Fact]
    public async Task Update_WhenCurrencyIsUnchanged_AllowsOtherChanges()
    {
        await using var database =
            await BusinessPartnerIntegrityTestDatabase.CreateAsync();
        await database.AddInvoiceAsync(isDeleted: false);
        var service = database.CreateService();

        var result = await service.UpdateAsync(
            1,
            CreateRequest(
                CurrencyCode.EGP,
                name: "Renamed Partner"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed Partner", result.Value.Name);
        Assert.Equal(CurrencyCode.EGP, result.Value.Currency);
    }

    [Fact]
    public async Task Update_WhenNoFinancialRecordsExist_AllowsCurrencyChange()
    {
        await using var database =
            await BusinessPartnerIntegrityTestDatabase.CreateAsync();
        var service = database.CreateService();

        var result = await service.UpdateAsync(
            1,
            CreateRequest(CurrencyCode.USD));

        Assert.True(result.IsSuccess);
        Assert.Equal(CurrencyCode.USD, result.Value.Currency);

        var partner = await database.GetPartnerAsync();
        Assert.Equal(CurrencyCode.USD, partner.Currency);
    }

    private static BusinessPartnerRequest CreateRequest(
        CurrencyCode currency,
        string name = "Partner One") =>
        new(
            name,
            null,
            null,
            null,
            null,
            currency,
            0m);

    private sealed class BusinessPartnerIntegrityTestDatabase
        : IAsyncDisposable
    {
        private BusinessPartnerIntegrityTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        private ApplicationDbContext Context { get; }

        public static async Task<BusinessPartnerIntegrityTestDatabase>
            CreateAsync()
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
            await SeedAsync(context);

            return new BusinessPartnerIntegrityTestDatabase(
                connection,
                context);
        }

        public BusinessPartnerService CreateService() =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(1));

        public Task AddInvoiceAsync(bool isDeleted) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO Invoices (
                     Id, CompanyId, BusinessPartnerId, IsDeleted)
                 VALUES (100, 1, 1, {isDeleted})
                 """);

        public Task AddOpeningBalanceAsync(bool isDeleted) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO PartnerOpeningBalances (
                     Id, CompanyId, BusinessPartnerId, IsDeleted)
                 VALUES (200, 1, 1, {isDeleted})
                 """);

        public async Task<BusinessPartner> GetPartnerAsync()
        {
            Context.ChangeTracker.Clear();

            return await Context.BusinessPartners
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(partner =>
                    partner.Id == 1 &&
                    partner.CompanyId == 1);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static async Task CreateSchemaAsync(
            ApplicationDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE BusinessPartners (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    PhoneNumber TEXT NULL,
                    Email TEXT NULL,
                    Address TEXT NULL,
                    TaxNumber TEXT NULL,
                    Currency INTEGER NOT NULL,
                    CreditLimit NUMERIC NOT NULL,
                    IsActive INTEGER NOT NULL,
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

                CREATE TABLE Stores (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NULL,
                    CashboxTransferId INTEGER NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Invoices (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    ContentType INTEGER NOT NULL DEFAULT 1,
                    BusinessPartnerId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE PartnerOpeningBalances (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE BusinessPartnerMovements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE ContainerMovements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE DriverTrips (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE CashVouchers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NULL,
                    IsDeleted INTEGER NOT NULL
                );
                """);
        }

        private static async Task SeedAsync(
            ApplicationDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO BusinessPartners (
                    Id, CompanyId, Code, Name, Currency, CreditLimit,
                    IsActive, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    1, 1, 'BP-1', 'Partner One', 1, 0,
                    1, 'test', '2026-01-01', 'test', 0);
                """);
        }
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
