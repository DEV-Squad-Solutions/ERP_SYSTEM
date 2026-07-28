using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.Items;
using MiniErp.Application.Features.ItemUnits;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Items;
using MiniErp.Infrastructure.Services.ItemUnits;
using MiniErp.Infrastructure.Services.Pagination;
using MiniErp.Infrastructure.Services.Stores;

namespace MiniErp.Tests.Inventory;

public sealed class InventoryMasterDeletionTests
{
    static InventoryMasterDeletionTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    public static TheoryData<string, bool, string> StoreDependencies =>
        new()
        {
            { "StoreContainer", false, "Stores.HasContainerAssignments" },
            { "StoreContainer", true, "Stores.HasContainerAssignments" },
            { "InvoiceStore", false, "Stores.HasDependencies" },
            { "InvoiceStore", true, "Stores.HasDependencies" },
            { "InvoiceContainerStore", false, "Stores.HasDependencies" },
            { "InvoiceContainerStore", true, "Stores.HasDependencies" },
            { "StockOpeningBalance", false, "Stores.HasDependencies" },
            { "StockOpeningBalance", true, "Stores.HasDependencies" },
            { "StockAdjustment", false, "Stores.HasDependencies" },
            { "StockAdjustment", true, "Stores.HasDependencies" },
            { "InventoryCount", false, "Stores.HasDependencies" },
            { "InventoryCount", true, "Stores.HasDependencies" },
            { "ItemMovement", false, "Stores.HasDependencies" },
            { "ItemMovement", true, "Stores.HasDependencies" },
            { "ContainerMovement", false, "Stores.HasDependencies" },
            { "ContainerMovement", true, "Stores.HasDependencies" }
        };

    public static TheoryData<string, bool> ItemDependencies =>
        new()
        {
            { "InvoiceLine", false },
            { "InvoiceLine", true },
            { "StockOpeningBalanceLine", false },
            { "StockOpeningBalanceLine", true },
            { "StockAdjustmentLine", false },
            { "StockAdjustmentLine", true },
            { "InventoryCountLine", false },
            { "InventoryCountLine", true },
            { "ItemMovement", false },
            { "ItemMovement", true }
        };

    public static TheoryData<string, bool> ItemUnitDependencies =>
        new()
        {
            { "Item", false },
            { "Item", true },
            { "InvoiceLine", false },
            { "InvoiceLine", true },
            { "StockOpeningBalanceLine", false },
            { "StockOpeningBalanceLine", true },
            { "StockAdjustmentLine", false },
            { "StockAdjustmentLine", true },
            { "InventoryCountLine", false },
            { "InventoryCountLine", true },
            { "ItemMovement", false },
            { "ItemMovement", true }
        };

    [Fact]
    public async Task GetAllItems_ReturnsOnlyCurrentCompanyItems()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemService(companyId: 1);

        var result = await service.GetAllAsync(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20
            });

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2, 4], result.Value.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetItemSelect_ReturnsActiveItemsWithActiveUnitsInStableOrder()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemService(companyId: 1);

        var result = await service.GetSelectAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2], result.Value.Select(item => item.Id));
    }

    [Fact]
    public async Task GetItemById_DoesNotReturnAnotherCompanyItem()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemService(companyId: 1);

        var result = await service.GetByIdAsync(3);

        Assert.True(result.IsFailure);
        Assert.Equal("Items.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddItem_TrimsValuesAndNormalizesBlankDescription()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemService(companyId: 1);

        var result = await service.AddAsync(
            new ItemRequest(
                1,
                "  NEW  ",
                "  New Item  ",
                "   "));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.CompanyId);
        Assert.Equal("NEW", result.Value.Code);
        Assert.Equal("New Item", result.Value.Name);
        Assert.Null(result.Value.Description);
    }

    [Fact]
    public async Task AddItem_RejectsNormalizedDuplicateCode()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemService(companyId: 1);

        var result = await service.AddAsync(
            new ItemRequest(
                1,
                "  ITEM-1  ",
                "Another Item",
                null));

        Assert.True(result.IsFailure);
        Assert.Equal("Items.CodeExists", result.Error.Code);
    }

    [Fact]
    public async Task AddItem_RejectsInactiveItemUnit()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemService(companyId: 1);

        var result = await service.AddAsync(
            new ItemRequest(
                5,
                "NEW",
                "New Item",
                null));

        Assert.True(result.IsFailure);
        Assert.Equal("ItemUnits.Inactive", result.Error.Code);
    }

    [Fact]
    public async Task UpdateItem_KeepsOwnCodeAndAppliesNormalizedValues()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemService(companyId: 1);

        var result = await service.UpdateAsync(
            1,
            new ItemRequest(
                2,
                "  ITEM-1  ",
                "  Updated Item  ",
                "  Updated description  "));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.ItemUnitId);
        Assert.Equal("ITEM-1", result.Value.Code);
        Assert.Equal("Updated Item", result.Value.Name);
        Assert.Equal("Updated description", result.Value.Description);
    }

    [Fact]
    public async Task ItemValidator_AcceptsValuesWhoseTrimmedLengthsAreValid()
    {
        var validator = new ItemRequestValidator();
        var request = new ItemRequest(
            1,
            $"  {new string('C', 50)}  ",
            $"  {new string('N', 200)}  ",
            $"  {new string('D', 1_000)}  ");

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ItemValidator_UsesSharedMaximumLengthRuleForTrimmedValue()
    {
        var validator = new ItemRequestValidator();
        var request = new ItemRequest(
            1,
            $"  {new string('C', 51)}  ",
            "Item",
            null);

        var result = await validator.ValidateAsync(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ItemRequest.Code), error.PropertyName);
        Assert.Equal("MaximumLengthValidator", error.ErrorCode);
    }

    [Fact]
    public async Task ItemValidator_ReturnsOneRequiredErrorPerWhitespaceValue()
    {
        var validator = new ItemRequestValidator();
        var request = new ItemRequest(
            1,
            "   ",
            "   ",
            null);

        var result = await validator.ValidateAsync(request);

        Assert.Equal(2, result.Errors.Count);
        Assert.All(
            result.Errors,
            error => Assert.Equal("NotEmptyValidator", error.ErrorCode));
        Assert.Equal(
            [nameof(ItemRequest.Code), nameof(ItemRequest.Name)],
            result.Errors.Select(error => error.PropertyName));
    }

    [Fact]
    public async Task GetAllItemUnits_ReturnsOnlyCurrentCompanyUnits()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemUnitService(companyId: 1);

        var result = await service.GetAllAsync(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20
            });

        Assert.True(result.IsSuccess);
        Assert.Equal([2, 5, 1, 3], result.Value.Items.Select(unit => unit.Id));
    }

    [Fact]
    public async Task GetItemUnitSelect_ReturnsOnlyActiveCurrentCompanyUnitsInOrder()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemUnitService(companyId: 1);

        var result = await service.GetSelectAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal([2, 1, 3], result.Value.Select(unit => unit.Id));
    }

    [Fact]
    public async Task GetItemUnitById_DoesNotReturnAnotherCompanyUnit()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemUnitService(companyId: 1);

        var result = await service.GetByIdAsync(4);

        Assert.True(result.IsFailure);
        Assert.Equal("ItemUnits.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddItemUnit_TrimsNameAndAssignsCurrentCompany()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemUnitService(companyId: 1);

        var result = await service.AddAsync(
            new ItemUnitRequest("  New Unit  "));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.CompanyId);
        Assert.Equal("New Unit", result.Value.Name);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task AddItemUnit_RejectsNormalizedDuplicateName()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemUnitService(companyId: 1);

        var result = await service.AddAsync(
            new ItemUnitRequest("  Primary Unit  "));

        Assert.True(result.IsFailure);
        Assert.Equal("ItemUnits.NameExists", result.Error.Code);
    }

    [Fact]
    public async Task UpdateItemUnit_KeepsOwnNameAndAppliesNormalizedValues()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemUnitService(companyId: 1);

        var result = await service.UpdateAsync(
            3,
            new ItemUnitRequest(
                "  Unused Unit  ",
                IsActive: false));

        Assert.True(result.IsSuccess);
        Assert.Equal("Unused Unit", result.Value.Name);
        Assert.False(result.Value.IsActive);
    }

    [Fact]
    public async Task ItemUnitValidator_AcceptsNameWhoseTrimmedLengthIsValid()
    {
        var validator = new ItemUnitRequestValidator();
        var request = new ItemUnitRequest(
            $"  {new string('N', 100)}  ");

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ItemUnitValidator_UsesSharedMaximumLengthRuleForTrimmedName()
    {
        var validator = new ItemUnitRequestValidator();
        var request = new ItemUnitRequest(
            $"  {new string('N', 101)}  ");

        var result = await validator.ValidateAsync(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ItemUnitRequest.Name), error.PropertyName);
        Assert.Equal("MaximumLengthValidator", error.ErrorCode);
    }

    [Fact]
    public async Task ItemUnitValidator_ReturnsOneRequiredErrorForWhitespaceName()
    {
        var validator = new ItemUnitRequestValidator();
        var request = new ItemUnitRequest("   ");

        var result = await validator.ValidateAsync(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ItemUnitRequest.Name), error.PropertyName);
        Assert.Equal("NotEmptyValidator", error.ErrorCode);
    }

    [Theory]
    [MemberData(nameof(StoreDependencies))]
    public async Task DeleteStore_BlocksCurrentAndHistoricalDependencies(
        string dependency,
        bool isDeleted,
        string expectedErrorCode)
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        await database.AddStoreDependencyAsync(dependency, isDeleted);
        var service = database.CreateStoreService(companyId: 1);

        var result = await service.DeleteAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedErrorCode, result.Error.Code);
        Assert.False((await database.GetStoreAsync(1)).IsDeleted);
    }

    [Fact]
    public async Task DeleteStore_WhenUnused_SoftDeletesIt()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateStoreService(companyId: 1);

        var result = await service.DeleteAsync(2);

        Assert.True(result.IsSuccess);
        var store = await database.GetStoreAsync(2);
        Assert.False(store.IsActive);
        Assert.True(store.IsDeleted);
    }

    [Fact]
    public async Task DeleteStore_DoesNotDeleteAnotherCompanyStore()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateStoreService(companyId: 1);

        var result = await service.DeleteAsync(3);

        Assert.True(result.IsFailure);
        Assert.Equal("Stores.NotFound", result.Error.Code);
        Assert.False((await database.GetStoreAsync(3)).IsDeleted);
    }

    [Theory]
    [MemberData(nameof(ItemDependencies))]
    public async Task DeleteItem_BlocksCurrentAndHistoricalDependencies(
        string dependency,
        bool isDeleted)
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        await database.AddItemDependencyAsync(dependency, isDeleted);
        var service = database.CreateItemService(companyId: 1);

        var result = await service.DeleteAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal("Items.InUse", result.Error.Code);
        Assert.False((await database.GetItemAsync(1)).IsDeleted);
    }

    [Fact]
    public async Task DeleteItem_WhenUnused_SoftDeletesIt()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemService(companyId: 1);

        var result = await service.DeleteAsync(2);

        Assert.True(result.IsSuccess);
        var item = await database.GetItemAsync(2);
        Assert.False(item.IsActive);
        Assert.True(item.IsDeleted);
    }

    [Fact]
    public async Task DeleteItem_DoesNotDeleteAnotherCompanyItem()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemService(companyId: 1);

        var result = await service.DeleteAsync(3);

        Assert.True(result.IsFailure);
        Assert.Equal("Items.NotFound", result.Error.Code);
        Assert.False((await database.GetItemAsync(3)).IsDeleted);
    }

    [Theory]
    [MemberData(nameof(ItemUnitDependencies))]
    public async Task DeleteItemUnit_BlocksCurrentAndHistoricalDependencies(
        string dependency,
        bool isDeleted)
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        await database.AddItemUnitDependencyAsync(dependency, isDeleted);
        var service = database.CreateItemUnitService(companyId: 1);

        var result = await service.DeleteAsync(2);

        Assert.True(result.IsFailure);
        Assert.Equal("ItemUnits.InUse", result.Error.Code);
        Assert.False((await database.GetItemUnitAsync(2)).IsDeleted);
    }

    [Fact]
    public async Task DeleteItemUnit_WhenUnused_SoftDeletesIt()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemUnitService(companyId: 1);

        var result = await service.DeleteAsync(3);

        Assert.True(result.IsSuccess);
        var itemUnit = await database.GetItemUnitAsync(3);
        Assert.False(itemUnit.IsActive);
        Assert.True(itemUnit.IsDeleted);
    }

    [Fact]
    public async Task DeleteItemUnit_DoesNotDeleteAnotherCompanyUnit()
    {
        await using var database = await InventoryDeletionDatabase.CreateAsync();
        var service = database.CreateItemUnitService(companyId: 1);

        var result = await service.DeleteAsync(4);

        Assert.True(result.IsFailure);
        Assert.Equal("ItemUnits.NotFound", result.Error.Code);
        Assert.False((await database.GetItemUnitAsync(4)).IsDeleted);
    }

    private sealed class InventoryDeletionDatabase : IAsyncDisposable
    {
        private InventoryDeletionDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        private ApplicationDbContext Context { get; }

        public static async Task<InventoryDeletionDatabase> CreateAsync()
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

            return new InventoryDeletionDatabase(connection, context);
        }

        public StoreService CreateStoreService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public ItemService CreateItemService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public ItemUnitService CreateItemUnitService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public Task AddStoreDependencyAsync(
            string dependency,
            bool isDeleted) =>
            dependency switch
            {
                "StoreContainer" => Context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO StoreContainers (
                         Id, CompanyId, StoreId, ContainerId, IsDeleted)
                     VALUES (100, 1, 1, 1, {isDeleted})
                     """),
                "InvoiceStore" => Context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO Invoices (
                         Id, CompanyId, StoreId, ContainerStoreId, IsDeleted)
                     VALUES (100, 1, 1, NULL, {isDeleted})
                     """),
                "InvoiceContainerStore" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO Invoices (
                             Id, CompanyId, StoreId, ContainerStoreId, IsDeleted)
                         VALUES (100, 1, 99, 1, {isDeleted})
                         """),
                "StockOpeningBalance" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO StockOpeningBalances (
                             Id, CompanyId, StoreId, IsDeleted)
                         VALUES (100, 1, 1, {isDeleted})
                         """),
                "StockAdjustment" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO StockAdjustments (
                             Id, CompanyId, StoreId, IsDeleted)
                         VALUES (100, 1, 1, {isDeleted})
                         """),
                "InventoryCount" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO InventoryCounts (
                             Id, CompanyId, StoreId, IsDeleted)
                         VALUES (100, 1, 1, {isDeleted})
                         """),
                "ItemMovement" => Context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO ItemMovements (
                         Id, CompanyId, StoreId, ItemId, ItemUnitId, IsDeleted)
                     VALUES (100, 1, 1, 1, 1, {isDeleted})
                     """),
                "ContainerMovement" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO ContainerMovements (
                             Id, CompanyId, ContainerStoreId, IsDeleted)
                         VALUES (100, 1, 1, {isDeleted})
                         """),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(dependency),
                    dependency,
                    null)
            };

        public Task AddItemDependencyAsync(
            string dependency,
            bool isDeleted) =>
            dependency switch
            {
                "InvoiceLine" => Context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO InvoiceLines (
                         Id, CompanyId, ItemId, ItemUnitId, IsDeleted)
                     VALUES (100, 1, 1, 1, {isDeleted})
                     """),
                "StockOpeningBalanceLine" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO StockOpeningBalanceLines (
                             Id, CompanyId, ItemId, ItemUnitId, IsDeleted)
                         VALUES (100, 1, 1, 1, {isDeleted})
                         """),
                "StockAdjustmentLine" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO StockAdjustmentLines (
                             Id, CompanyId, ItemId, ItemUnitId, IsDeleted)
                         VALUES (100, 1, 1, 1, {isDeleted})
                         """),
                "InventoryCountLine" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO InventoryCountLines (
                             Id, CompanyId, ItemId, ItemUnitId, IsDeleted)
                         VALUES (100, 1, 1, 1, {isDeleted})
                         """),
                "ItemMovement" => Context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO ItemMovements (
                         Id, CompanyId, StoreId, ItemId, ItemUnitId, IsDeleted)
                     VALUES (100, 1, 1, 1, 1, {isDeleted})
                     """),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(dependency),
                    dependency,
                    null)
            };

        public Task AddItemUnitDependencyAsync(
            string dependency,
            bool isDeleted) =>
            dependency switch
            {
                "Item" => Context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO Items (
                         Id, CompanyId, ItemUnitId, Code, Name, IsActive,
                         CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                     VALUES (
                         100, 1, 2, 'DEPENDENT', 'Dependent Item', 1,
                         'system', '2026-07-26', 'test', {isDeleted})
                     """),
                "InvoiceLine" => Context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO InvoiceLines (
                         Id, CompanyId, ItemId, ItemUnitId, IsDeleted)
                     VALUES (100, 1, 1, 2, {isDeleted})
                     """),
                "StockOpeningBalanceLine" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO StockOpeningBalanceLines (
                             Id, CompanyId, ItemId, ItemUnitId, IsDeleted)
                         VALUES (100, 1, 1, 2, {isDeleted})
                         """),
                "StockAdjustmentLine" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO StockAdjustmentLines (
                             Id, CompanyId, ItemId, ItemUnitId, IsDeleted)
                         VALUES (100, 1, 1, 2, {isDeleted})
                         """),
                "InventoryCountLine" =>
                    Context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO InventoryCountLines (
                             Id, CompanyId, ItemId, ItemUnitId, IsDeleted)
                         VALUES (100, 1, 1, 2, {isDeleted})
                         """),
                "ItemMovement" => Context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO ItemMovements (
                         Id, CompanyId, StoreId, ItemId, ItemUnitId, IsDeleted)
                     VALUES (100, 1, 1, 1, 2, {isDeleted})
                     """),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(dependency),
                    dependency,
                    null)
            };

        public async Task<Store> GetStoreAsync(int storeId)
        {
            Context.ChangeTracker.Clear();

            return await Context.Stores
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(store => store.Id == storeId);
        }

        public async Task<Item> GetItemAsync(int itemId)
        {
            Context.ChangeTracker.Clear();

            return await Context.Items
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.Id == itemId);
        }

        public async Task<ItemUnit> GetItemUnitAsync(int itemUnitId)
        {
            Context.ChangeTracker.Clear();

            return await Context.ItemUnits
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(itemUnit => itemUnit.Id == itemUnitId);
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
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE ItemUnits (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
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

                CREATE TABLE Items (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    ItemUnitId INTEGER NOT NULL,
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
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    ContainerId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Invoices (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
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
                    ItemId INTEGER NOT NULL,
                    ItemUnitId INTEGER NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE ContainerMovements (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    ContainerStoreId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE InvoiceLines (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    ItemUnitId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE StockOpeningBalanceLines (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    ItemUnitId INTEGER NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE StockAdjustmentLines (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    ItemUnitId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE InventoryCountLines (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    ItemUnitId INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );
                """);
        }

        private static async Task SeedAsync(ApplicationDbContext context)
        {
            context.ItemUnits.AddRange(
                CreateItemUnit(1, 1, "Primary Unit"),
                CreateItemUnit(2, 1, "Historical Unit"),
                CreateItemUnit(3, 1, "Unused Unit"),
                CreateItemUnit(4, 2, "Other Company Unit"),
                CreateItemUnit(5, 1, "Inactive Unit", isActive: false));

            context.Items.AddRange(
                CreateItem(1, 1, 1, "ITEM-1", "Shared Item"),
                CreateItem(2, 1, 1, "ITEM-2", "Shared Item"),
                CreateItem(3, 2, 4, "ITEM-3", "Shared Item"),
                CreateItem(
                    4,
                    1,
                    1,
                    "ITEM-4",
                    "Shared Item",
                    isActive: false));

            context.Stores.AddRange(
                CreateStore(1, 1, "STORE-1", "Referenced Store"),
                CreateStore(2, 1, "STORE-2", "Unused Store"),
                CreateStore(3, 2, "STORE-3", "Other Company Store"));

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        private static ItemUnit CreateItemUnit(
            int id,
            int companyId,
            string name,
            bool isActive = true) =>
            new()
            {
                Id = id,
                CompanyId = companyId,
                Name = name,
                IsActive = isActive
            };

        private static Item CreateItem(
            int id,
            int companyId,
            int itemUnitId,
            string code,
            string name,
            bool isActive = true) =>
            new()
            {
                Id = id,
                CompanyId = companyId,
                ItemUnitId = itemUnitId,
                Code = code,
                Name = name,
                IsActive = isActive
            };

        private static Store CreateStore(
            int id,
            int companyId,
            string code,
            string name) =>
            new()
            {
                Id = id,
                CompanyId = companyId,
                Code = code,
                Name = name
            };
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
