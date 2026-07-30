using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Inventory;
using MiniErp.Infrastructure.Services.InventoryCounts;
using MiniErp.Infrastructure.Services.Pagination;
using MiniErp.Infrastructure.Services.StockAdjustments;

namespace MiniErp.Tests.Inventory;

internal sealed class InventoryDocumentTestDatabase : IAsyncDisposable
{
    private InventoryDocumentTestDatabase(
        SqliteConnection connection,
        ApplicationDbContext context)
    {
        Connection = connection;
        Context = context;
    }

    private SqliteConnection Connection { get; }

    public ApplicationDbContext Context { get; }

    public static async Task<InventoryDocumentTestDatabase> CreateAsync()
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

        return new InventoryDocumentTestDatabase(connection, context);
    }

    public StockAdjustmentService CreateStockAdjustmentService(
        int companyId = 1)
    {
        var currentCompany = new TestCurrentCompanyContext(companyId);
        var stockService = new InventoryStockService(
            Context,
            currentCompany);
        var costingService = new InventoryCostingService(
            Context,
            currentCompany,
            TimeProvider.System);

        return new StockAdjustmentService(
            Context,
            new PaginationService(),
            currentCompany,
            stockService,
            costingService,
            TimeProvider.System);
    }

    public InventoryCostReportService CreateInventoryCostReportService(
        int companyId = 1) =>
        new(
            Context,
            new TestCurrentCompanyContext(companyId));

    public InventoryCountService CreateInventoryCountService(
        int companyId = 1)
    {
        var currentCompany = new TestCurrentCompanyContext(companyId);
        var stockService = new InventoryStockService(
            Context,
            currentCompany);
        var costingService = new InventoryCostingService(
            Context,
            currentCompany,
            TimeProvider.System);

        return new InventoryCountService(
            Context,
            new PaginationService(),
            currentCompany,
            stockService,
            costingService,
            TimeProvider.System);
    }

    public Task SetStockBalanceCheckModeAsync(
        StockBalanceCheckMode mode,
        int companyId = 1) =>
        Context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CompanySettings (CompanyId, StockBalanceCheckMode) VALUES ({companyId}, {(int)mode}) ON CONFLICT(CompanyId) DO UPDATE SET StockBalanceCheckMode = excluded.StockBalanceCheckMode;");

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
                StockBalanceCheckMode INTEGER NOT NULL DEFAULT 1,
                BaseCurrency INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE
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

            CREATE TABLE StockOpeningBalances (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                StoreId INTEGER NOT NULL,
                DocumentNumber TEXT NOT NULL,
                DocumentDate TEXT NOT NULL,
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

            CREATE TABLE StockOpeningBalanceLines (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                StockOpeningBalanceId INTEGER NOT NULL,
                ItemId INTEGER NOT NULL,
                ItemUnitId INTEGER NULL,
                Count INTEGER NOT NULL,
                Weight NUMERIC NOT NULL,
                Quantity NUMERIC NOT NULL,
                Price NUMERIC NOT NULL,
                Total NUMERIC NOT NULL,
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

            CREATE TABLE ItemMovements (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                StoreId INTEGER NOT NULL,
                ItemId INTEGER NOT NULL,
                ItemUnitId INTEGER NULL,
                MovementType INTEGER NOT NULL,
                ReferenceId INTEGER NOT NULL,
                ReferenceNumber TEXT NOT NULL,
                MovementDate TEXT NOT NULL,
                QuantityIn NUMERIC NOT NULL,
                QuantityOut NUMERIC NOT NULL,
                CostStatus INTEGER NOT NULL DEFAULT 1,
                PendingCostQuantity NUMERIC NOT NULL DEFAULT 0,
                UnitCost NUMERIC NULL,
                TotalCost NUMERIC NOT NULL DEFAULT 0,
                QuantityAfter NUMERIC NOT NULL DEFAULT 0,
                AverageCostAfter NUMERIC NOT NULL DEFAULT 0,
                InventoryValueAfter NUMERIC NOT NULL DEFAULT 0,
                Description TEXT NULL,
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

            CREATE UNIQUE INDEX UX_ItemMovements_Company_Id
            ON ItemMovements (CompanyId, Id);

            CREATE TABLE InventoryCostAllocations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                StoreId INTEGER NOT NULL,
                ItemId INTEGER NOT NULL,
                OutboundMovementId INTEGER NOT NULL,
                InboundMovementId INTEGER NOT NULL,
                Quantity NUMERIC NOT NULL,
                UnitCost NUMERIC NOT NULL,
                TotalCost NUMERIC NOT NULL,
                CreatedOn TEXT NOT NULL
            );

            CREATE UNIQUE INDEX UX_InventoryCostAllocations_Pair
            ON InventoryCostAllocations (
                CompanyId,
                OutboundMovementId,
                InboundMovementId);

            CREATE TABLE ItemStoreBalances (
                CompanyId INTEGER NOT NULL,
                StoreId INTEGER NOT NULL,
                ItemId INTEGER NOT NULL,
                Quantity NUMERIC NOT NULL DEFAULT 0,
                AverageCost NUMERIC NOT NULL DEFAULT 0,
                InventoryValue NUMERIC NOT NULL DEFAULT 0,
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
                IsDeleted INTEGER NOT NULL,
                PRIMARY KEY (CompanyId, StoreId, ItemId)
            );

            CREATE TABLE InventoryCounts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                StoreId INTEGER NOT NULL,
                DocumentNumber TEXT NOT NULL,
                CountDate TEXT NOT NULL,
                SnapshotTakenAt TEXT NOT NULL,
                ReconciledAt TEXT NULL,
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

            CREATE UNIQUE INDEX UX_InventoryCounts_Company_Document
            ON InventoryCounts (CompanyId, DocumentNumber)
            WHERE IsDeleted = 0;

            CREATE TABLE InventoryCountLines (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                InventoryCountId INTEGER NOT NULL,
                ItemId INTEGER NOT NULL,
                ItemUnitId INTEGER NOT NULL,
                SystemQuantity NUMERIC NOT NULL,
                PhysicalQuantity NUMERIC NULL,
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

            CREATE UNIQUE INDEX UX_InventoryCountLines_Company_Count_Item
            ON InventoryCountLines (CompanyId, InventoryCountId, ItemId)
            WHERE IsDeleted = 0;

            CREATE TABLE StockAdjustments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                StoreId INTEGER NOT NULL,
                DocumentNumber TEXT NOT NULL,
                DocumentDate TEXT NOT NULL,
                Direction INTEGER NOT NULL,
                Reason TEXT NULL,
                SourceInventoryCountId INTEGER NULL,
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

            CREATE UNIQUE INDEX UX_StockAdjustments_Company_Document
            ON StockAdjustments (CompanyId, DocumentNumber)
            WHERE IsDeleted = 0;

            CREATE UNIQUE INDEX UX_StockAdjustments_Company_Count_Direction
            ON StockAdjustments (
                CompanyId,
                SourceInventoryCountId,
                Direction)
            WHERE SourceInventoryCountId IS NOT NULL AND IsDeleted = 0;

            CREATE TABLE StockAdjustmentLines (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                StockAdjustmentId INTEGER NOT NULL,
                ItemId INTEGER NOT NULL,
                ItemUnitId INTEGER NOT NULL,
                Quantity NUMERIC NOT NULL CHECK (Quantity > 0),
                UnitCost NUMERIC NULL,
                Reason TEXT NULL,
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

            CREATE UNIQUE INDEX UX_StockAdjustmentLines_Company_Adjustment_Item
            ON StockAdjustmentLines (
                CompanyId,
                StockAdjustmentId,
                ItemId)
            WHERE IsDeleted = 0;

            CREATE TRIGGER AdvanceStockAdjustmentRowVersion
            AFTER UPDATE ON StockAdjustments
            BEGIN
                UPDATE StockAdjustments
                SET RowVersion = randomblob(8)
                WHERE Id = NEW.Id;
            END;

            CREATE TRIGGER AdvanceInventoryCountRowVersion
            AFTER UPDATE ON InventoryCounts
            BEGIN
                UPDATE InventoryCounts
                SET RowVersion = randomblob(8)
                WHERE Id = NEW.Id;
            END;
            """);
    }

    private static async Task SeedAsync(ApplicationDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Companies (
                Id, Name, Address, CommercialRegister, TaxNumber,
                ManagerName, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 'Company A', '', 'CR-A', 'TX-A', 'Manager',
                 'test', '2026-01-01', 'test', 0),
                (2, 'Company B', '', 'CR-B', 'TX-B', 'Manager',
                 'test', '2026-01-01', 'test', 0);

            INSERT INTO Stores (
                Id, CompanyId, BusinessPartnerId, Code, Name, Address,
                IsContainerStore, IsActive, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES
                (1, 1, NULL, 'MAIN', 'Main Store', NULL,
                 0, 1, 'test', '2026-01-01', 'test', 0),
                (2, 1, NULL, 'CONT', 'Container Store', NULL,
                 1, 1, 'test', '2026-01-01', 'test', 0),
                (3, 2, NULL, 'OTHER', 'Other Store', NULL,
                 0, 1, 'test', '2026-01-01', 'test', 0);

            INSERT INTO CompanySettings (CompanyId, StockBalanceCheckMode, BaseCurrency)
            VALUES (1, 1, 0), (2, 1, 0);

            INSERT INTO ItemUnits (
                Id, CompanyId, Name, IsActive, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 'Piece', 1, 'test', '2026-01-01', 'test', 0),
                (2, 2, 'Piece', 1, 'test', '2026-01-01', 'test', 0);

            INSERT INTO Items (
                Id, CompanyId, ItemUnitId, Code, Name, Description,
                IsActive, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 1, 'ITEM-1', 'Item One', NULL,
                 1, 'test', '2026-01-01', 'test', 0),
                (2, 1, 1, 'ITEM-2', 'Item Two', NULL,
                 1, 'test', '2026-01-01', 'test', 0),
                (3, 2, 2, 'ITEM-3', 'Other Item', NULL,
                 1, 'test', '2026-01-01', 'test', 0);

            INSERT INTO StockOpeningBalances (
                Id, CompanyId, StoreId, DocumentNumber, DocumentDate,
                Notes, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 1, 'OPEN-1', '2026-01-01',
                 NULL, 'test', '2026-01-01', 'test', 0);

            INSERT INTO StockOpeningBalanceLines (
                Id, CompanyId, StockOpeningBalanceId, ItemId, ItemUnitId,
                Count, Weight, Quantity, Price, Total, Notes,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 1, 1, 1, 1, 10, 10, 0, 0, NULL,
                 'test', '2026-01-01', 'test', 0);

            INSERT INTO ItemMovements (
                Id, CompanyId, StoreId, ItemId, ItemUnitId, MovementType,
                ReferenceId, ReferenceNumber, MovementDate, QuantityIn,
                QuantityOut, CostStatus, PendingCostQuantity, UnitCost,
                TotalCost, QuantityAfter, AverageCostAfter,
                InventoryValueAfter, Description, CreatedById, CreatedOn,
                CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 1, 1, 1, 5, 1, 'OPEN-1', '2026-01-01', 10,
                 0, 1, 0, 0, 0, 10, 0, 0, 'Opening balance OPEN-1',
                 'test', '2026-01-01', 'test', 0);
            """);
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
