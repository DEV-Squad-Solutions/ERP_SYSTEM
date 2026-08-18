using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.Countries;
using MiniErp.Domain.Entities.ReferenceData;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Countries;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.Countries;

public sealed class CountryServiceTests
{
    static CountryServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task GetAll_ReturnsNewestGlobalActiveAndInactiveCountriesFirst()
    {
        await using var database = await CountryTestDatabase.CreateAsync();
        var service = database.CreateService();

        var result = await service.GetAllAsync(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20
            });

        Assert.True(result.IsSuccess);
        Assert.Equal([3, 2, 1], result.Value.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetSelect_ReturnsOnlyActiveCountriesWithArabicNames()
    {
        await using var database = await CountryTestDatabase.CreateAsync();
        var service = database.CreateService();

        var result = await service.GetSelectAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal([3, 1], result.Value.Select(country => country.Id));
        Assert.Equal(["ألف", "باء"], result.Value.Select(country => country.Name));
    }

    [Fact]
    public async Task GetAll_FiltersArabicNameAndEnglishNameSeparately()
    {
        await using var database = await CountryTestDatabase.CreateAsync();
        var service = database.CreateService();
        var pagination = new PaginationRequest
        {
            PageNumber = 1,
            PageSize = 20
        };

        var arabicResult = await service.GetAllAsync(
            pagination,
            new CountryFilterRequest(Name: "ألف"));
        var englishResult = await service.GetAllAsync(
            pagination,
            new CountryFilterRequest(EnglishName: "Zulu"));

        Assert.True(arabicResult.IsSuccess);
        Assert.Equal(3, Assert.Single(arabicResult.Value.Items).Id);
        Assert.True(englishResult.IsSuccess);
        Assert.Equal(3, Assert.Single(englishResult.Value.Items).Id);
    }

    [Fact]
    public async Task Add_TrimsValues()
    {
        await using var database = await CountryTestDatabase.CreateAsync();
        var service = database.CreateService();

        var result = await service.AddAsync(
            new CountryRequest(
                "  دولة جديدة  ",
                "  New Country  "));

        Assert.True(result.IsSuccess);
        Assert.Matches(
            "^CTR-[0-9]{4,}$",
            result.Value.Code);
        Assert.Equal("دولة جديدة", result.Value.Name);
        Assert.Equal("New Country", result.Value.EnglishName);
    }

    [Fact]
    public async Task Add_GeneratesAUniqueGlobalCode()
    {
        await using var database = await CountryTestDatabase.CreateAsync();
        var service = database.CreateService();

        var result = await service.AddAsync(
            new CountryRequest(
                "دولة أخرى",
                "Another Country"));

        Assert.True(result.IsSuccess);
        Assert.NotEqual("EG", result.Value.Code);
    }

    [Fact]
    public async Task Update_ReactivatesAndPreservesTheGeneratedCode()
    {
        await using var database = await CountryTestDatabase.CreateAsync();
        var service = database.CreateService();

        var addResult = await service.AddAsync(
            new CountryRequest(
                "دولة غير نشطة",
                "Inactive Duplicate",
                IsActive: false));

        Assert.True(addResult.IsSuccess);
        var generatedCode = addResult.Value.Code;

        var updateResult = await service.UpdateAsync(
            addResult.Value.Id,
            new CountryRequest(
                "دولة غير نشطة",
                "Inactive Duplicate",
                IsActive: true));

        Assert.True(updateResult.IsSuccess);
        Assert.Equal(generatedCode, updateResult.Value.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_WhenInvoiceExists_BlocksCurrentAndHistoricalReferences(
        bool isDeleted)
    {
        await using var database = await CountryTestDatabase.CreateAsync();
        await database.AddInvoiceAsync(countryId: 1, isDeleted);
        var service = database.CreateService();

        var result = await service.DeleteAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal("Countries.HasInvoices", result.Error.Code);
        Assert.False((await database.GetCountryAsync(1)).IsDeleted);
    }

    [Fact]
    public async Task Delete_WhenCountryIsUnused_SoftDeletesIt()
    {
        await using var database = await CountryTestDatabase.CreateAsync();
        var service = database.CreateService();

        var result = await service.DeleteAsync(2);

        Assert.True(result.IsSuccess);
        var country = await database.GetCountryAsync(2);
        Assert.False(country.IsActive);
        Assert.True(country.IsDeleted);
    }

    [Fact]
    public async Task Validator_AcceptsValuesWhoseTrimmedLengthsAreValid()
    {
        var validator = new CountryRequestValidator();
        var request = new CountryRequest(
            $"  {new string('ع', 200)}  ",
            $"  {new string('N', 200)}  ");

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    private sealed class CountryTestDatabase : IAsyncDisposable
    {
        private CountryTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        private ApplicationDbContext Context { get; }

        public static async Task<CountryTestDatabase> CreateAsync()
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

            return new CountryTestDatabase(connection, context);
        }

        public CountryService CreateService() =>
            new(Context, new PaginationService());

        public Task AddInvoiceAsync(int countryId, bool isDeleted) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO Invoices (Id, CountryId, IsDeleted)
                 VALUES (100, {countryId}, {isDeleted})
                 """);

        public async Task<Country> GetCountryAsync(int countryId)
        {
            Context.ChangeTracker.Clear();

            return await Context.Countries
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(country => country.Id == countryId);
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
                CREATE TABLE Countries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    EnglishName TEXT NOT NULL,
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

                CREATE UNIQUE INDEX UX_Countries_Code_Active
                ON Countries (Code)
                WHERE IsActive = 1 AND IsDeleted = 0;

                CREATE TABLE Invoices (
                    Id INTEGER PRIMARY KEY,
                    ContentType INTEGER NOT NULL DEFAULT 1,
                    CountryId INTEGER NULL,
                    IsDeleted INTEGER NOT NULL
                );
                """);
        }

        private static async Task SeedAsync(ApplicationDbContext context)
        {
            context.Countries.AddRange(
                CreateCountry(
                    id: 1,
                    code: "EG",
                    name: "باء",
                    englishName: "Active Country",
                    isActive: true),
                CreateCountry(
                    id: 2,
                    code: "OLD",
                    name: "جيم",
                    englishName: "Inactive Country",
                    isActive: false),
                CreateCountry(
                    id: 3,
                    code: "AA",
                    name: "ألف",
                    englishName: "Zulu Country",
                    isActive: true));
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        private static Country CreateCountry(
            int id,
            string code,
            string name,
            string englishName,
            bool isActive) =>
            new()
            {
                Id = id,
                Code = code,
                Name = name,
                EnglishName = englishName,
                IsActive = isActive
            };
    }
}
