using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.BusinessPartners;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.BusinessPartners;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.BusinessPartners;

public sealed class BusinessPartnerContainerStoreServiceTests
{
    static BusinessPartnerContainerStoreServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task GetContainerStore_ReturnsOnlyContainersAssignedToStore()
    {
        await using var database =
            await BusinessPartnerContainerStoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.GetContainerStoreAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.ContainerStore.Id);
        Assert.Equal(1, result.Value.ContainerStore.BusinessPartnerId);
        Assert.Equal("Partner One", result.Value.ContainerStore.BusinessPartnerName);

        var container = Assert.Single(result.Value.Containers);
        Assert.Equal(100, container.Id);
        Assert.True(container.IsAssigned);
        Assert.Equal(1_000, container.StoreContainerId);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyActiveContainersAssignedToPartnerStore()
    {
        await using var database =
            await BusinessPartnerContainerStoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.GetAllAsync(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20
            });

        Assert.True(result.IsSuccess);
        var partner = Assert.Single(
            result.Value.Items,
            item => item.Id == 1);
        var container = Assert.Single(partner.Containers!);
        Assert.Equal(100, container.Id);
        Assert.True(container.IsAssigned);
        Assert.Equal(1_000, container.StoreContainerId);

        var partnerWithoutStore = Assert.Single(
            result.Value.Items,
            item => item.Id == 2);
        Assert.Null(partnerWithoutStore.ContainerStore);
        Assert.Empty(partnerWithoutStore.Containers!);
    }

    [Fact]
    public async Task GetById_WorkspaceIncludesInactiveAssignedContainer()
    {
        await using var database =
            await BusinessPartnerContainerStoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.GetByIdAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Equal([100, 101, 102], result.Value.Containers!
            .Select(container => container.Id)
            .Order()
            .ToArray());

        var inactiveAssigned = Assert.Single(
            result.Value.Containers!,
            container => container.Id == 102);
        Assert.False(inactiveAssigned.IsActive);
        Assert.True(inactiveAssigned.IsAssigned);
        Assert.Equal(1_001, inactiveAssigned.StoreContainerId);
    }

    [Fact]
    public async Task GetContainerStore_DoesNotExposeAnotherCompanyPartner()
    {
        await using var database =
            await BusinessPartnerContainerStoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.GetContainerStoreAsync(3);

        Assert.True(result.IsFailure);
        Assert.Equal("BusinessPartners.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetContainerStore_WhenPartnerHasNoActiveStore_ReturnsNotFound()
    {
        await using var database =
            await BusinessPartnerContainerStoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.GetContainerStoreAsync(2);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "BusinessPartners.ContainerStoreNotFound",
            result.Error.Code);
    }

    [Fact]
    public async Task Add_RejectsCaseInsensitiveDuplicateName()
    {
        await using var database =
            await BusinessPartnerContainerStoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new BusinessPartnerRequest(
                "partner one",
                null,
                null,
                null,
                null,
                CurrencyCode.EGP,
                0m));

        Assert.True(result.IsFailure);
        Assert.Equal("BusinessPartners.NameExists", result.Error.Code);
    }

    [Fact]
    public async Task Add_GeneratesAUniqueCompanyScopedCode()
    {
        await using var database =
            await BusinessPartnerContainerStoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new BusinessPartnerRequest(
                "Unique Partner Name",
                null,
                null,
                null,
                null,
                CurrencyCode.EGP,
                0m));

        Assert.True(result.IsSuccess);
        Assert.Matches(
            "^BPR-[0-9]{4,}$",
            result.Value.Code);
        Assert.NotEqual("BP-1", result.Value.Code);
    }

    [Fact]
    public async Task Add_RejectsCaseInsensitiveDuplicateTaxNumber()
    {
        await using var database =
            await BusinessPartnerContainerStoreTestDatabase.CreateAsync();
        await database.SetTaxNumberAsync("TAX-001");
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new BusinessPartnerRequest(
                "Unique Partner Name",
                null,
                null,
                null,
                "tax-001",
                CurrencyCode.EGP,
                0m));

        Assert.True(result.IsFailure);
        Assert.Equal("BusinessPartners.TaxNumberExists", result.Error.Code);
    }

    [Fact]
    public async Task Update_RejectsAnotherPartnersCaseInsensitiveDuplicateName()
    {
        await using var database =
            await BusinessPartnerContainerStoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpdateAsync(
            2,
            new BusinessPartnerRequest(
                "partner one",
                null,
                null,
                null,
                null,
                CurrencyCode.EGP,
                0m));

        Assert.True(result.IsFailure);
        Assert.Equal("BusinessPartners.NameExists", result.Error.Code);
    }

    [Fact]
    public async Task Update_PreservesTheStoredCode()
    {
        await using var database =
            await BusinessPartnerContainerStoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpdateAsync(
            2,
            new BusinessPartnerRequest(
                "Partner Without Store",
                null,
                null,
                null,
                null,
                CurrencyCode.EGP,
                0m));

        Assert.True(result.IsSuccess);
        Assert.Equal("BP-2", result.Value.Code);
    }

    [Fact]
    public async Task Update_RejectsAnotherPartnersCaseInsensitiveDuplicateTaxNumber()
    {
        await using var database =
            await BusinessPartnerContainerStoreTestDatabase.CreateAsync();
        await database.SetTaxNumberAsync("TAX-001");
        var service = database.CreateService(companyId: 1);

        var result = await service.UpdateAsync(
            2,
            new BusinessPartnerRequest(
                "Partner Without Store",
                null,
                null,
                null,
                "tax-001",
                CurrencyCode.EGP,
                0m));

        Assert.True(result.IsFailure);
        Assert.Equal("BusinessPartners.TaxNumberExists", result.Error.Code);
    }

    [Fact]
    public async Task Update_AllowsCaseOnlyChangesToOwnUniqueValues()
    {
        await using var database =
            await BusinessPartnerContainerStoreTestDatabase.CreateAsync();
        await database.SetTaxNumberAsync("TAX-001");
        var service = database.CreateService(companyId: 1);

        var result = await service.UpdateAsync(
            1,
            new BusinessPartnerRequest(
                "partner one",
                null,
                null,
                null,
                "tax-001",
                CurrencyCode.EGP,
                0m));

        Assert.True(result.IsSuccess);
        Assert.Equal("BP-1", result.Value.Code);
        Assert.Equal("partner one", result.Value.Name);
        Assert.Equal("tax-001", result.Value.TaxNumber);
    }

    private sealed class BusinessPartnerContainerStoreTestDatabase
        : IAsyncDisposable
    {
        private BusinessPartnerContainerStoreTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        private ApplicationDbContext Context { get; }

        public static async Task<BusinessPartnerContainerStoreTestDatabase>
            CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);

            await CreateSchemaAsync(context);
            await SeedAsync(context);

            return new BusinessPartnerContainerStoreTestDatabase(
                connection,
                context);
        }

        public BusinessPartnerService CreateService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public Task SetTaxNumberAsync(string taxNumber) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE BusinessPartners SET TaxNumber = {taxNumber} WHERE Id = 1");

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
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Address TEXT NULL,
                    IsContainerStore INTEGER NOT NULL,
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

                CREATE TABLE Containers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
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

                CREATE TABLE StoreContainers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    ContainerId INTEGER NOT NULL,
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
                """);
        }

        private static async Task SeedAsync(ApplicationDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO BusinessPartners (
                    Id, CompanyId, Code, Name, Currency, CreditLimit,
                    IsActive, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 'BP-1', 'Partner One', 1, 0,
                     1, 'test', '2026-01-01', 'test', 0),
                    (2, 1, 'BP-2', 'Partner Without Store', 1, 0,
                     1, 'test', '2026-01-01', 'test', 0),
                    (3, 2, 'BP-3', 'Other Company Partner', 1, 0,
                     1, 'test', '2026-01-01', 'test', 0);

                INSERT INTO Stores (
                    Id, CompanyId, BusinessPartnerId, Code, Name, Address,
                    IsContainerStore, IsActive, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES
                    (10, 1, 1, 'CONT-1', 'Partner One Container Store', NULL,
                     1, 1, 'test', '2026-01-01', 'test', 0),
                    (20, 2, 3, 'CONT-2', 'Other Company Container Store', NULL,
                     1, 1, 'test', '2026-01-01', 'test', 0);

                INSERT INTO Containers (
                    Id, CompanyId, Code, Name, Description, IsActive,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (100, 1, 'C-1', 'Assigned Container', NULL, 1,
                     'test', '2026-01-01', 'test', 0),
                    (101, 1, 'C-2', 'Available Container', NULL, 1,
                     'test', '2026-01-01', 'test', 0),
                    (102, 1, 'C-3', 'Inactive Container', NULL, 0,
                     'test', '2026-01-01', 'test', 0),
                    (200, 2, 'C-4', 'Other Company Container', NULL, 1,
                     'test', '2026-01-01', 'test', 0);

                INSERT INTO StoreContainers (
                    Id, CompanyId, StoreId, ContainerId, IsActive,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1000, 1, 10, 100, 1,
                     'test', '2026-01-01', 'test', 0),
                    (1001, 1, 10, 102, 1,
                     'test', '2026-01-01', 'test', 0),
                    (2000, 2, 20, 200, 1,
                     'test', '2026-01-01', 'test', 0);
                """);
        }
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
