using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Features.Stores;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Pagination;
using MiniErp.Infrastructure.Services.Stores;

namespace MiniErp.Tests.Stores;

public sealed class StoreServiceTests
{
    public static TheoryData<string, bool> HistoricalRoleDependencies =>
        new()
        {
            { "InvoiceProductStore", false },
            { "InvoiceProductStore", true },
            { "StockOpeningBalance", false },
            { "StockOpeningBalance", true },
            { "StockAdjustment", false },
            { "StockAdjustment", true },
            { "InventoryCount", false },
            { "InventoryCount", true },
            { "ItemMovement", false },
            { "ItemMovement", true },
            { "StockTransferSource", false },
            { "StockTransferSource", true },
            { "StockTransferDestination", false },
            { "StockTransferDestination", true },
            { "InvoiceContainerStore", false },
            { "InvoiceContainerStore", true },
            { "ContainerMovement", false },
            { "ContainerMovement", true }
        };

    static StoreServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task Add_ProductStore_NormalizesAndSavesRequest()
    {
        await using var database = await StoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new StoreRequest(
                " New Product Store ",
                " Main Address ",
                false,
                null));

        Assert.True(result.IsSuccess);
        Assert.Matches(
            "^STR-[0-9]{4,}$",
            result.Value.Code);
        Assert.Equal("New Product Store", result.Value.Name);
        Assert.Equal("Main Address", result.Value.Address);
        Assert.False(result.Value.IsContainerStore);
        Assert.Null(result.Value.BusinessPartnerId);
    }

    [Fact]
    public async Task Add_ContainerStore_WithActivePartner_Succeeds()
    {
        await using var database = await StoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new StoreRequest(
                "New Container Store",
                null,
                true,
                2));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsContainerStore);
        Assert.Equal(2, result.Value.BusinessPartnerId);
        Assert.Equal("Partner Two", result.Value.BusinessPartnerName);
    }

    [Fact]
    public async Task Add_ContainerStore_RejectsOtherCompanyPartner()
    {
        await using var database = await StoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new StoreRequest(
                "Other Company Partner Store",
                null,
                true,
                4));

        Assert.True(result.IsFailure);
        Assert.Equal("Stores.BusinessPartnerNotFound", result.Error.Code);
    }

    [Fact]
    public async Task Add_ContainerStore_RejectsInactivePartner()
    {
        await using var database = await StoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new StoreRequest(
                "Inactive Partner Store",
                null,
                true,
                3));

        Assert.True(result.IsFailure);
        Assert.Equal("Stores.BusinessPartnerInactive", result.Error.Code);
    }

    [Fact]
    public async Task Add_ActiveContainerStore_RejectsSecondStoreForPartner()
    {
        await using var database = await StoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new StoreRequest(
                "Second Container Store",
                null,
                true,
                1));

        Assert.True(result.IsFailure);
        Assert.Equal("Stores.ActiveContainerStoreExists", result.Error.Code);
    }

    [Fact]
    public async Task Selectors_ReturnOnlyUsableStoresForEachRole()
    {
        await using var database = await StoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var productStores = await service.GetSelectAsync();
        var containerStores = await service.GetContainerSelectAsync();

        Assert.True(productStores.IsSuccess);
        Assert.Equal(
            [10, 14],
            productStores.Value.Select(store => store.Id).Order().ToArray());

        Assert.True(containerStores.IsSuccess);
        var containerStore = Assert.Single(containerStores.Value);
        Assert.Equal(11, containerStore.Id);
    }

    [Fact]
    public async Task Update_PreservesTheStoredCode()
    {
        await using var database = await StoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpdateAsync(
            14,
            new StoreRequest(
                "Unused Product Store",
                null,
                false,
                null));

        Assert.True(result.IsSuccess);
        Assert.Equal("PROD-UNUSED", result.Value.Code);
    }

    [Theory]
    [MemberData(nameof(HistoricalRoleDependencies))]
    public async Task Update_IdentityChange_BlocksCurrentAndHistoricalRoleRecords(
        string dependency,
        bool isDeleted)
    {
        await using var database = await StoreTestDatabase.CreateAsync();
        await database.AddRoleDependencyAsync(dependency, isDeleted);
        var service = database.CreateService(companyId: 1);

        var changesProductStoreRole = dependency is
            "InvoiceProductStore" or
            "StockOpeningBalance" or
            "StockAdjustment" or
            "InventoryCount" or
            "ItemMovement" or
            "StockTransferSource" or
            "StockTransferDestination";
        var storeId = changesProductStoreRole ? 10 : 11;
        var request = changesProductStoreRole
            ? new StoreRequest(
                "Product Store",
                null,
                true,
                2)
            : new StoreRequest(
                "Partner One Container Store",
                null,
                true,
                2);

        var result = await service.UpdateAsync(storeId, request);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "Stores.HistoricalIdentityChangeNotAllowed",
            result.Error.Code);
    }

    [Fact]
    public async Task Update_ChangingContainerStoreType_BlocksContainerHistory()
    {
        await using var database = await StoreTestDatabase.CreateAsync();
        await database.AddRoleDependencyAsync(
            "InvoiceContainerStore",
            isDeleted: true);
        var service = database.CreateService(companyId: 1);

        var result = await service.UpdateAsync(
            11,
            new StoreRequest(
                "Partner One Container Store",
                null,
                false,
                null));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "Stores.HistoricalIdentityChangeNotAllowed",
            result.Error.Code);
    }

    [Fact]
    public async Task Update_IdentityChange_BlocksHistoricalContainerAssignment()
    {
        await using var database = await StoreTestDatabase.CreateAsync();
        await database.AddRoleDependencyAsync(
            "StoreContainer",
            isDeleted: true);
        var service = database.CreateService(companyId: 1);

        var result = await service.UpdateAsync(
            11,
            new StoreRequest(
                "Partner One Container Store",
                null,
                true,
                2));

        Assert.True(result.IsFailure);
        Assert.Equal("Stores.HasContainerAssignments", result.Error.Code);
    }

    [Fact]
    public async Task Update_TypeChangeWithoutHistory_Succeeds()
    {
        await using var database = await StoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpdateAsync(
            14,
            new StoreRequest(
                "Unused Product Store",
                null,
                true,
                2));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsContainerStore);
        Assert.Equal(2, result.Value.BusinessPartnerId);
    }

    [Fact]
    public async Task Update_PartnerChangeWithoutHistory_Succeeds()
    {
        await using var database = await StoreTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpdateAsync(
            11,
            new StoreRequest(
                "Partner One Container Store",
                null,
                true,
                2));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.BusinessPartnerId);
        Assert.Equal("Partner Two", result.Value.BusinessPartnerName);
    }

    private sealed class StoreTestDatabase : IAsyncDisposable
    {
        private StoreTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        private ApplicationDbContext Context { get; }

        public static async Task<StoreTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection(
                "Data Source=:memory:;Foreign Keys=True");
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

            return new StoreTestDatabase(connection, context);
        }

        public StoreService CreateService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public Task AddRoleDependencyAsync(
            string dependency,
            bool isDeleted)
        {
            var deletedValue = isDeleted ? 1 : 0;
            return dependency switch
            {
                "InvoiceProductStore" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO Invoices (
                             Id, CompanyId, StoreId, ContainerStoreId, IsDeleted)
                         VALUES (1, 1, 10, NULL, {deletedValue});
                         """),
                "InvoiceContainerStore" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO Invoices (
                             Id, CompanyId, StoreId, ContainerStoreId, IsDeleted)
                         VALUES (1, 1, 10, 11, {deletedValue});
                         """),
                "StockOpeningBalance" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO StockOpeningBalances (
                             Id, CompanyId, StoreId, IsDeleted)
                         VALUES (1, 1, 10, {deletedValue});
                         """),
                "StockAdjustment" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO StockAdjustments (
                             Id, CompanyId, StoreId, IsDeleted)
                         VALUES (1, 1, 10, {deletedValue});
                         """),
                "InventoryCount" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO InventoryCounts (
                             Id, CompanyId, StoreId, IsDeleted)
                         VALUES (1, 1, 10, {deletedValue});
                         """),
                "ItemMovement" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO ItemMovements (
                             Id, CompanyId, StoreId, IsDeleted)
                         VALUES (1, 1, 10, {deletedValue});
                         """),
                "StockTransferSource" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO StockTransfers (
                             Id, CompanyId, SourceStoreId,
                             DestinationStoreId, IsDeleted)
                         VALUES (1, 1, 10, 14, {deletedValue});
                         """),
                "StockTransferDestination" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO StockTransfers (
                             Id, CompanyId, SourceStoreId,
                             DestinationStoreId, IsDeleted)
                         VALUES (1, 1, 14, 10, {deletedValue});
                         """),
                "ContainerMovement" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO ContainerMovements (
                             Id, CompanyId, ContainerStoreId, IsDeleted)
                         VALUES (1, 1, 11, {deletedValue});
                         """),
                "StoreContainer" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO StoreContainers (
                             Id, CompanyId, StoreId, IsDeleted)
                         VALUES (1, 1, 11, {deletedValue});
                         """),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(dependency),
                    dependency,
                    "Unsupported Store dependency.")
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static Task CreateSchemaAsync(ApplicationDbContext context) =>
            context.Database.ExecuteSqlRawAsync(
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
                    IsDeleted INTEGER NOT NULL,
                    UNIQUE (CompanyId, Id)
                );

                CREATE TABLE Stores (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Address TEXT NULL,
                    IsContainerStore INTEGER NOT NULL DEFAULT 0,
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
                    IsDeleted INTEGER NOT NULL,
                    UNIQUE (CompanyId, Id),
                    CHECK (
                        (IsContainerStore = 0 AND BusinessPartnerId IS NULL) OR
                        (IsContainerStore = 1 AND BusinessPartnerId IS NOT NULL)
                    ),
                    FOREIGN KEY (CompanyId, BusinessPartnerId)
                        REFERENCES BusinessPartners (CompanyId, Id)
                        ON DELETE RESTRICT
                );

                CREATE UNIQUE INDEX UX_Stores_CompanyId_Code
                ON Stores (CompanyId, Code)
                WHERE IsDeleted = 0;

                CREATE UNIQUE INDEX
                    UX_Stores_CompanyId_BusinessPartnerId_ActiveContainer
                ON Stores (CompanyId, BusinessPartnerId)
                WHERE BusinessPartnerId IS NOT NULL
                  AND IsContainerStore = 1
                  AND IsActive = 1
                  AND IsDeleted = 0;

                CREATE TABLE StoreContainers (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Invoices (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    ContentType INTEGER NOT NULL DEFAULT 1,
                    StoreId INTEGER NOT NULL,
                    ContainerStoreId INTEGER NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE StockOpeningBalances (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE StockAdjustments (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE InventoryCounts (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE ItemMovements (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE StockTransfers (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    SourceStoreId INTEGER NOT NULL,
                    DestinationStoreId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE ContainerMovements (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    ContainerStoreId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );
                """);

        private static Task SeedAsync(ApplicationDbContext context) =>
            context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO BusinessPartners (
                    Id, CompanyId, Code, Name, Currency, CreditLimit,
                    IsActive, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 'BP-1', 'Partner One', 1, 0,
                     1, 'test', '2026-01-01', 'test', 0),
                    (2, 1, 'BP-2', 'Partner Two', 1, 0,
                     1, 'test', '2026-01-01', 'test', 0),
                    (3, 1, 'BP-3', 'Inactive Partner', 1, 0,
                     0, 'test', '2026-01-01', 'test', 0),
                    (4, 2, 'BP-4', 'Other Company Partner', 1, 0,
                     1, 'test', '2026-01-01', 'test', 0);

                INSERT INTO Stores (
                    Id, CompanyId, BusinessPartnerId, Code, Name, Address,
                    IsContainerStore, IsActive, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES
                    (10, 1, NULL, 'PROD-1', 'Product Store', NULL,
                     0, 1, 'test', '2026-01-01', 'test', 0),
                    (11, 1, 1, 'CONT-1', 'Partner One Container Store', NULL,
                     1, 1, 'test', '2026-01-01', 'test', 0),
                    (12, 1, 2, 'CONT-2', 'Inactive Container Store', NULL,
                     1, 0, 'test', '2026-01-01', 'test', 0),
                    (13, 1, NULL, 'PROD-INACTIVE', 'Inactive Product Store', NULL,
                     0, 0, 'test', '2026-01-01', 'test', 0),
                    (14, 1, NULL, 'PROD-UNUSED', 'Unused Product Store', NULL,
                     0, 1, 'test', '2026-01-01', 'test', 0),
                    (15, 1, 3, 'CONT-INACTIVE-PARTNER',
                     'Inactive Partner Container Store', NULL,
                     1, 1, 'test', '2026-01-01', 'test', 0),
                    (20, 2, 4, 'CONT-OTHER', 'Other Company Container Store', NULL,
                     1, 1, 'test', '2026-01-01', 'test', 0);
                """);
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
