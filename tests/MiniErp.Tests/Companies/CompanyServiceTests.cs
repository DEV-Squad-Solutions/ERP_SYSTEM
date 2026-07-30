using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Companies;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Companies;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.Companies;

public sealed class CompanyServiceTests
{
    private static readonly Guid CurrentUserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    static CompanyServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task Add_TrimsValuesAndAssignsTheCurrentUser()
    {
        await using var database = await CompanyTestDatabase.CreateAsync();
        var service = database.CreateService(CurrentUserId);

        var result = await service.AddAsync(
            new CompanyRequest(
                "  New Company  ",
                "  New Address  ",
                "  CR-NEW  ",
                "  TAX-NEW  ",
                "  New Manager  "));

        Assert.True(result.IsSuccess);
        Assert.Equal("New Company", result.Value.Name);
        Assert.Equal("New Address", result.Value.Address);
        Assert.Equal("CR-NEW", result.Value.CommercialRegister);
        Assert.Equal("TAX-NEW", result.Value.TaxNumber);
        Assert.Equal("New Manager", result.Value.ManagerName);
        Assert.Equal(StockBalanceCheckMode.DateCheck, result.Value.StockBalanceCheckMode);

        var assignment = await database.Context.UserCompanies
            .AsNoTracking()
            .SingleAsync(userCompany =>
                userCompany.CompanyId == result.Value.Id);
        Assert.Equal(CurrentUserId, assignment.UserId);
    }

    [Fact]
    public async Task AddAndUpdate_PersistTheCompanyStockBalanceCheckMode()
    {
        await using var database = await CompanyTestDatabase.CreateAsync();
        var service = database.CreateService(CurrentUserId);

        var added = await service.AddAsync(
            new CompanyRequest(
                "New Company",
                "New Address",
                "CR-NEW",
                "TAX-NEW",
                "New Manager",
                StockBalanceCheckMode.Both));

        Assert.True(added.IsSuccess);
        Assert.Equal(StockBalanceCheckMode.Both, added.Value.StockBalanceCheckMode);
        Assert.Equal(
            StockBalanceCheckMode.Both,
            await database.Context.CompanySettings
                .Where(settings => settings.CompanyId == added.Value.Id)
                .Select(settings => settings.StockBalanceCheckMode)
                .SingleAsync());

        var updated = await service.UpdateAsync(
            added.Value.Id,
            new CompanyRequest(
                "Updated Company",
                "Updated Address",
                "CR-NEW",
                "TAX-NEW",
                "Updated Manager",
                StockBalanceCheckMode.FinalCheck));

        Assert.True(updated.IsSuccess);
        Assert.Equal(StockBalanceCheckMode.FinalCheck, updated.Value.StockBalanceCheckMode);
    }

    [Fact]
    public async Task GetAll_ReturnsTheStockBalanceCheckMode()
    {
        await using var database = await CompanyTestDatabase.CreateAsync();
        var service = database.CreateService(CurrentUserId);
        var created = await service.AddAsync(
            new CompanyRequest(
                "Configured Company",
                "Address",
                "CR-CONFIGURED",
                "TAX-CONFIGURED",
                "Manager",
                StockBalanceCheckMode.None));

        var page = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 });

        Assert.True(created.IsSuccess);
        Assert.True(page.IsSuccess);
        var response = page.Value.Items.Single(item => item.Id == created.Value.Id);
        Assert.Equal(StockBalanceCheckMode.None, response.StockBalanceCheckMode);
    }

    [Fact]
    public async Task Add_RejectsDuplicateNormalizedCommercialRegister()
    {
        await using var database = await CompanyTestDatabase.CreateAsync();
        var service = database.CreateService(CurrentUserId);

        var result = await service.AddAsync(
            new CompanyRequest(
                "New Company",
                "New Address",
                "  CR-1  ",
                "TAX-NEW",
                "New Manager"));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "Companies.CommercialRegisterExists",
            result.Error.Code);
    }

    [Fact]
    public async Task Update_TrimsValuesAndAllowsUnchangedUniqueValues()
    {
        await using var database = await CompanyTestDatabase.CreateAsync();
        var service = database.CreateService(CurrentUserId);

        var result = await service.UpdateAsync(
            1,
            new CompanyRequest(
                "  Updated Company  ",
                "  Updated Address  ",
                "  CR-1  ",
                "  TAX-1  ",
                "  Updated Manager  "));

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Company", result.Value.Name);
        Assert.Equal("Updated Address", result.Value.Address);
        Assert.Equal("CR-1", result.Value.CommercialRegister);
        Assert.Equal("TAX-1", result.Value.TaxNumber);
        Assert.Equal("Updated Manager", result.Value.ManagerName);
    }

    [Fact]
    public async Task GetById_DoesNotReturnSoftDeletedCompany()
    {
        await using var database = await CompanyTestDatabase.CreateAsync();
        await database.SetDeletedAsync(1);
        var service = database.CreateService(CurrentUserId);

        var result = await service.GetByIdAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal("Companies.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Delete_WhenHistoricalDependencyExists_ReturnsConflict()
    {
        await using var database = await CompanyTestDatabase.CreateAsync();
        await database.AddHistoricalItemAsync(companyId: 2);
        var service = database.CreateService(CurrentUserId);

        var result = await service.DeleteAsync(2);

        Assert.True(result.IsFailure);
        Assert.Equal("Companies.HasDependencies", result.Error.Code);
        Assert.False((await database.GetCompanyAsync(2)).IsDeleted);
    }

    [Fact]
    public async Task Delete_WhenCompanyIsUnused_SoftDeletesIt()
    {
        await using var database = await CompanyTestDatabase.CreateAsync();
        var service = database.CreateService(CurrentUserId);

        var result = await service.DeleteAsync(2);

        Assert.True(result.IsSuccess);
        Assert.True((await database.GetCompanyAsync(2)).IsDeleted);
    }

    [Fact]
    public async Task Validator_AcceptsValuesWhoseTrimmedLengthsAreValid()
    {
        var validator = new CompanyRequestValidator();
        var request = new CompanyRequest(
            $"  {new string('N', 200)}  ",
            $"  {new string('A', 500)}  ",
            $"  {new string('C', 50)}  ",
            $"  {new string('T', 50)}  ",
            $"  {new string('M', 200)}  ");

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_RejectsValueWhoseTrimmedLengthIsTooLong()
    {
        var validator = new CompanyRequestValidator();
        var request = new CompanyRequest(
            $"  {new string('N', 201)}  ",
            "Address",
            "CR",
            "TAX",
            "Manager");

        var result = await validator.ValidateAsync(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(CompanyRequest.Name), error.PropertyName);
    }

    private sealed class CompanyTestDatabase : IAsyncDisposable
    {
        private CompanyTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        public ApplicationDbContext Context { get; }

        public static async Task<CompanyTestDatabase> CreateAsync()
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

            return new CompanyTestDatabase(connection, context);
        }

        public CompanyService CreateService(Guid currentUserId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentUserService(currentUserId));

        public Task AddHistoricalItemAsync(int companyId) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO Items (Id, CompanyId, IsDeleted)
                 VALUES (100, {companyId}, 1)
                 """);

        public async Task SetDeletedAsync(int companyId)
        {
            var company = await Context.Companies.SingleAsync(
                entity => entity.Id == companyId);
            company.IsDeleted = true;
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        public async Task<Company> GetCompanyAsync(int companyId)
        {
            Context.ChangeTracker.Clear();

            return await Context.Companies
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(company => company.Id == companyId);
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
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE CompanySettings (
                    CompanyId INTEGER NOT NULL PRIMARY KEY,
                    BaseCurrency INTEGER NOT NULL DEFAULT 1,
                    StockBalanceCheckMode INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE
                );

                CREATE TABLE UserCompanies (
                    UserId TEXT NOT NULL,
                    CompanyId INTEGER NOT NULL,
                    PRIMARY KEY (UserId, CompanyId)
                );

                CREATE TABLE Items (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE ItemUnits (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE BusinessPartners (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Drivers (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Stores (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Containers (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE StoreContainers (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Cashboxes (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE CashMovementTypes (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE CashVouchers (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );
                """);
        }

        private static async Task SeedAsync(ApplicationDbContext context)
        {
            context.Companies.AddRange(
                CreateCompany(
                    id: 1,
                    name: "Company One",
                    commercialRegister: "CR-1",
                    taxNumber: "TAX-1"),
                CreateCompany(
                    id: 2,
                    name: "Company Two",
                    commercialRegister: "CR-2",
                    taxNumber: "TAX-2"));
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        private static Company CreateCompany(
            int id,
            string name,
            string commercialRegister,
            string taxNumber) =>
            new()
            {
                Id = id,
                Name = name,
                Address = $"{name} Address",
                CommercialRegister = commercialRegister,
                TaxNumber = taxNumber,
                ManagerName = $"{name} Manager"
            };
    }

    private sealed class TestCurrentUserService(Guid userId)
        : ICurrentUserService
    {
        public Result<Guid> GetUserId() => Result<Guid>.Success(userId);
    }
}
