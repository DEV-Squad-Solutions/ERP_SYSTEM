using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.ItemsCategories;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.ItemsCategories;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.ItemsCategories;

public sealed class ItemsCategoryServiceTests
{
    static ItemsCategoryServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task GetAllAndSelect_EnforceOrderingTenantAndActiveRules()
    {
        await using var database = await CategoryTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var page = await service.GetAllAsync(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20
            });
        var select = await service.GetSelectAsync();

        Assert.True(page.IsSuccess);
        Assert.Equal([2, 1], page.Value.Items.Select(item => item.Id));
        Assert.True(select.IsSuccess);
        Assert.Equal([1], select.Value.Select(item => item.Id));
    }

    [Fact]
    public async Task Add_TrimsValuesAndRejectsActiveDuplicate()
    {
        await using var database = await CategoryTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var added = await service.AddAsync(
            new ItemsCategoryRequest(
                "  Export Items  ",
                Notes: "  Invoice header category  "));
        var duplicate = await service.AddAsync(
            new ItemsCategoryRequest(" export items "));

        Assert.True(added.IsSuccess);
        Assert.Equal("Export Items", added.Value.Name);
        Assert.Equal("Invoice header category", added.Value.Notes);
        Assert.True(duplicate.IsFailure);
        Assert.Equal("ItemsCategories.NameExists", duplicate.Error.Code);
    }

    [Fact]
    public async Task Update_UsesOriginalRowVersionAndReturnsNewToken()
    {
        await using var database = await CategoryTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var original = (await service.GetByIdAsync(1)).Value;

        var updated = await service.UpdateAsync(
            1,
            new ItemsCategoryUpdateRequest(
                "Updated Category",
                true,
                null,
                original.RowVersion));
        var stale = await service.UpdateAsync(
            1,
            new ItemsCategoryUpdateRequest(
                "Stale Category",
                true,
                null,
                original.RowVersion));

        Assert.True(updated.IsSuccess);
        Assert.False(original.RowVersion.SequenceEqual(
            updated.Value.RowVersion));
        Assert.True(stale.IsFailure);
        Assert.Equal("ItemsCategories.Concurrency", stale.Error.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_BlocksCurrentOrHistoricalInvoiceReferences(
        bool isDeleted)
    {
        await using var database = await CategoryTestDatabase.CreateAsync();
        await database.AddInvoiceAsync(
            companyId: 1,
            categoryId: 1,
            isDeleted);
        var service = database.CreateService(companyId: 1);

        var result = await service.DeleteAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal("ItemsCategories.HasInvoices", result.Error.Code);
    }

    [Fact]
    public async Task OtherCompanyCategory_IsNotVisibleOrMutable()
    {
        await using var database = await CategoryTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var get = await service.GetByIdAsync(3);
        var update = await service.UpdateAsync(
            3,
            new ItemsCategoryUpdateRequest(
                "Changed",
                true,
                null,
                new byte[8]));
        var delete = await service.DeleteAsync(3);

        Assert.Equal("ItemsCategories.NotFound", get.Error.Code);
        Assert.Equal("ItemsCategories.NotFound", update.Error.Code);
        Assert.Equal("ItemsCategories.NotFound", delete.Error.Code);
    }

    [Fact]
    public void UpdateValidator_RequiresEightByteToken()
    {
        var categoryValidator = new ItemsCategoryUpdateRequestValidator();
        var invalidCategory = categoryValidator.Validate(
            new ItemsCategoryUpdateRequest(
                "Category",
                true,
                null,
                [1, 2]));

        Assert.Contains(
            invalidCategory.Errors,
            error =>
                error.PropertyName ==
                nameof(ItemsCategoryUpdateRequest.RowVersion));
    }

    private sealed class CategoryTestDatabase : IAsyncDisposable
    {
        private CategoryTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        private ApplicationDbContext Context { get; }

        public static async Task<CategoryTestDatabase> CreateAsync()
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

            return new CategoryTestDatabase(connection, context);
        }

        public ItemsCategoryService CreateService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public Task AddInvoiceAsync(
            int companyId,
            int categoryId,
            bool isDeleted) =>
            Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO Invoices (
                     Id, CompanyId, ItemsCategoryId, IsDeleted)
                 VALUES (100, {companyId}, {categoryId}, {isDeleted})
                 """);

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static Task CreateSchemaAsync(
            ApplicationDbContext context) =>
            context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE ItemsCategories (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    Notes TEXT NULL,
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

                CREATE UNIQUE INDEX UX_ItemsCategories_ActiveName
                ON ItemsCategories (CompanyId, Name)
                WHERE IsActive = 1 AND IsDeleted = 0;

                CREATE TABLE Invoices (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    ItemsCategoryId INTEGER NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TRIGGER AdvanceItemsCategoryRowVersion
                AFTER UPDATE ON ItemsCategories
                BEGIN
                    UPDATE ItemsCategories
                    SET RowVersion = randomblob(8)
                    WHERE Id = NEW.Id;
                END;
                """);

        private static Task SeedAsync(ApplicationDbContext context) =>
            context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO ItemsCategories (
                    Id, CompanyId, Name, IsActive, Notes, RowVersion,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 'Active Category', 1, NULL, randomblob(8),
                     'test', '2026-01-01', 'test', 0),
                    (2, 1, 'Inactive Category', 0, NULL, randomblob(8),
                     'test', '2026-01-01', 'test', 0),
                    (3, 2, 'Other Company Category', 1, NULL, randomblob(8),
                     'test', '2026-01-01', 'test', 0);
                """);
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
