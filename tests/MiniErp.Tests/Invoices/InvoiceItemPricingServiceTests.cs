using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.InvoiceItemPricing;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.InvoiceItemPricing;

namespace MiniErp.Tests.Invoices;

public sealed class InvoiceItemPricingServiceTests
{
    [Fact]
    public async Task ExpensesAreAdvisoryPerInvoiceLineAndDoNotChangeInventoryCost()
    {
        await using var database = await PricingTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var replaceResult = await service.ReplaceExpensesAsync(
            invoiceLineId: 1,
            new ReplaceInvoiceLinePricingExpensesRequest(
                Expenses:
                [
                    new InvoiceLinePricingExpenseRequest(
                        Name: "نقل",
                        Amount: 15m),
                    new InvoiceLinePricingExpenseRequest(
                        Name: "تحميل",
                        Amount: 5m,
                        Notes: "لأغراض التسعير فقط")
                ]));

        Assert.True(replaceResult.IsSuccess, replaceResult.Error.Description);
        var row = replaceResult.Value;
        Assert.Equal(1, row.InvoiceLineId);
        Assert.Equal(1, row.InvoiceId);
        Assert.Equal("INV-001", row.InvoiceNumber);
        Assert.Equal(1, row.ItemId);
        Assert.Equal("ITEM-1", row.ItemCode);
        Assert.Equal(10m, row.Quantity);
        Assert.Equal(12m, row.AverageCost);
        Assert.Equal(20m, row.ManualExpensesTotal);
        Assert.Equal(2m, row.ManualExpensesPerUnit);
        Assert.Equal(14m, row.IndicativeUnitCost);
        Assert.Equal(140m, row.IndicativeTotalCost);
        Assert.Equal(2, row.Expenses.Count);

        var movement = await database.Context.ItemMovements
            .AsNoTracking()
            .Where(entity => entity.CompanyId == 1 && entity.Id == 1)
            .Select(entity => new
            {
                entity.UnitCost,
                entity.AverageCostAfter
            })
            .SingleAsync();
        Assert.Equal(12m, movement.UnitCost);
        Assert.Equal(11m, movement.AverageCostAfter);
    }

    [Fact]
    public async Task ReportReturnsEveryInvoiceItemForCurrentCompanyOnly()
    {
        await using var database = await PricingTestDatabase.CreateAsync();

        var companyOneResult = await database.CreateService(companyId: 1)
            .GetAsync(
                new PaginationRequest { PageNumber = 1, PageSize = 20 },
                new InvoiceItemPricingFilterRequest());
        var companyTwoResult = await database.CreateService(companyId: 2)
            .GetAsync(
                new PaginationRequest { PageNumber = 1, PageSize = 20 },
                new InvoiceItemPricingFilterRequest());

        Assert.True(companyOneResult.IsSuccess, companyOneResult.Error.Description);
        Assert.True(companyTwoResult.IsSuccess, companyTwoResult.Error.Description);
        Assert.Single(companyOneResult.Value.Items);
        Assert.Single(companyTwoResult.Value.Items);
        Assert.Equal("INV-001", companyOneResult.Value.Items[0].InvoiceNumber);
        Assert.Equal("INV-OTHER", companyTwoResult.Value.Items[0].InvoiceNumber);
        Assert.Equal(CurrencyCode.EGP, companyOneResult.Value.BaseCurrency);
        Assert.Equal(CurrencyCode.USD, companyTwoResult.Value.BaseCurrency);
    }

    private sealed class PricingTestDatabase : IAsyncDisposable
    {
        private PricingTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        public ApplicationDbContext Context { get; }

        public static async Task<PricingTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var interceptor = new AuditableEntityInterceptor(
                new HttpContextAccessor(),
                TimeProvider.System);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(interceptor)
                .Options;
            var context = new ApplicationDbContext(options);

            await CreateSchemaAndSeedAsync(context);
            return new PricingTestDatabase(connection, context);
        }

        public InvoiceItemPricingService CreateService(int companyId) =>
            new(Context, new TestCurrentCompanyContext(companyId));

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static async Task CreateSchemaAndSeedAsync(
            ApplicationDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE Companies (
                    Id INTEGER PRIMARY KEY,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE CompanySettings (
                    CompanyId INTEGER PRIMARY KEY,
                    BaseCurrency INTEGER NOT NULL,
                    StockBalanceCheckMode INTEGER NOT NULL
                );

                CREATE TABLE BusinessPartners (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    Special INTEGER NOT NULL DEFAULT 0,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Stores (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE ItemUnits (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Items (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    ItemUnitId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Invoices (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    InvoiceNumber TEXT NOT NULL,
                    InvoiceDate TEXT NOT NULL,
                    InvoiceType INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    Currency INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE InvoiceLines (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    InvoiceId INTEGER NOT NULL,
                    ItemId INTEGER NULL,
                    ItemUnitId INTEGER NULL,
                    Quantity NUMERIC NOT NULL,
                    Price NUMERIC NOT NULL,
                    BaseUnitPrice NUMERIC NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE ItemMovements (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    ReferenceId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    MovementType INTEGER NOT NULL,
                    CostStatus INTEGER NOT NULL,
                    UnitCost NUMERIC NULL,
                    AverageCostAfter NUMERIC NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE InvoiceLinePricingExpenses (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    InvoiceLineId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    Amount NUMERIC NOT NULL,
                    Notes TEXT NULL,
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

                INSERT INTO Companies (Id, IsDeleted) VALUES (1, 0), (2, 0);
                INSERT INTO CompanySettings (CompanyId, BaseCurrency, StockBalanceCheckMode)
                VALUES (1, 1, 1), (2, 2, 1);
                INSERT INTO BusinessPartners (Id, CompanyId, Name, IsDeleted)
                VALUES (1, 1, 'Partner 1', 0), (2, 2, 'Partner 2', 0);
                INSERT INTO Stores (Id, CompanyId, Name, IsDeleted)
                VALUES (1, 1, 'Store 1', 0), (2, 2, 'Store 2', 0);
                INSERT INTO ItemUnits (Id, CompanyId, Name, IsDeleted)
                VALUES (1, 1, 'Piece', 0), (2, 2, 'Unit', 0);
                INSERT INTO Items (Id, CompanyId, ItemUnitId, Code, Name, IsDeleted)
                VALUES
                    (1, 1, 1, 'ITEM-1', 'Item 1', 0),
                    (2, 2, 2, 'ITEM-2', 'Item 2', 0);
                INSERT INTO Invoices (
                    Id, CompanyId, InvoiceNumber, InvoiceDate, InvoiceType,
                    BusinessPartnerId, StoreId, Currency, IsDeleted)
                VALUES
                    (1, 1, 'INV-001', '2026-08-01', 1, 1, 1, 1, 0),
                    (2, 2, 'INV-OTHER', '2026-08-02', 2, 2, 2, 2, 0);
                INSERT INTO InvoiceLines (
                    Id, CompanyId, InvoiceId, ItemId, ItemUnitId, Quantity,
                    Price, BaseUnitPrice, IsDeleted)
                VALUES
                    (1, 1, 1, 1, 1, 10, 30, 30, 0),
                    (2, 2, 2, 2, 2, 4, 20, 40, 0);
                INSERT INTO ItemMovements (
                    Id, CompanyId, ReferenceId, ItemId, MovementType,
                    CostStatus, UnitCost, AverageCostAfter, IsDeleted)
                VALUES
                    (1, 1, 1, 1, 1, 1, 12, 11, 0),
                    (2, 2, 2, 2, 3, 1, 8, 9, 0);
                """);
        }
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
