using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Cashboxes;
using MiniErp.Infrastructure.Services.CashMovementTypes;
using MiniErp.Infrastructure.Services.CashVouchers;
using MiniErp.Infrastructure.Services.DriverTrips;
using MiniErp.Infrastructure.Services.Pagination;
using MiniErp.Infrastructure.Services.Statements;

namespace MiniErp.Tests.CashManagement;

internal sealed class CashManagementTestDatabase : IAsyncDisposable
{
    private CashManagementTestDatabase(
        SqliteConnection connection,
        DbContextOptions<ApplicationDbContext> options,
        ApplicationDbContext context)
    {
        Connection = connection;
        Options = options;
        Context = context;
    }

    private SqliteConnection Connection { get; }

    private DbContextOptions<ApplicationDbContext> Options { get; }

    public ApplicationDbContext Context { get; }

    public static async Task<CashManagementTestDatabase> CreateAsync()
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

        await CreateSchemaAsync(context);
        await SeedAsync(context);
        context.ChangeTracker.Clear();

        return new CashManagementTestDatabase(
            connection,
            options,
            context);
    }

    public ApplicationDbContext CreateAdditionalContext() => new(Options);

    public CashboxService CreateCashboxService(
        int companyId,
        ApplicationDbContext? context = null) =>
        new(
            context ?? Context,
            new PaginationService(),
            new TestCurrentCompanyContext(companyId),
            new MiniErp.Tests.TestExchangeRateResolver(),
            TimeProvider.System);

    public CashMovementTypeService CreateMovementTypeService(
        int companyId,
        ApplicationDbContext? context = null) =>
        new(
            context ?? Context,
            new PaginationService(),
            new TestCurrentCompanyContext(companyId));

    public CashVoucherService CreateVoucherService(
        int companyId,
        ApplicationDbContext? context = null) =>
        new(
            context ?? Context,
            new PaginationService(),
            new TestCurrentCompanyContext(companyId),
            new MiniErp.Tests.TestExchangeRateResolver(),
            TimeProvider.System);

    public DriverTripService CreateDriverTripService(
        int companyId,
        ApplicationDbContext? context = null) =>
        new(
            context ?? Context,
            new PaginationService(),
            new TestCurrentCompanyContext(companyId));

    public FinancialStatementService CreateStatementService(
        int companyId,
        ApplicationDbContext? context = null) =>
        new(
            context ?? Context,
            new TestCurrentCompanyContext(companyId));

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
                IsActive INTEGER NOT NULL DEFAULT 1,
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

            CREATE TABLE Drivers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                Code TEXT NOT NULL,
                Name TEXT NOT NULL,
                PhoneNumber TEXT NULL,
                NationalId TEXT NULL,
                LicenseNumber TEXT NOT NULL,
                LicenseExpiryDate TEXT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
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

            CREATE TABLE Invoices (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                InvoiceNumber TEXT NOT NULL,
                PartnerInvoiceNo TEXT NULL,
                ContentType INTEGER NOT NULL DEFAULT 1,
                IsDeleted INTEGER NOT NULL
            );

            CREATE TABLE PartnerOpeningBalances (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                BusinessPartnerId INTEGER NOT NULL,
                DocumentNumber TEXT NOT NULL,
                DocumentDate TEXT NOT NULL,
                Currency INTEGER NOT NULL,
                ExchangeRateId INTEGER NULL,
                ExchangeRate NUMERIC NOT NULL DEFAULT 1,
                BalanceType INTEGER NOT NULL,
                Amount NUMERIC NOT NULL,
                BaseAmount NUMERIC NOT NULL DEFAULT 0,
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

            CREATE TABLE Cashboxes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                Code TEXT NOT NULL COLLATE NOCASE,
                Name TEXT NOT NULL COLLATE NOCASE,
                Currency INTEGER NOT NULL,
                OpeningBalance NUMERIC NOT NULL,
                OpeningBalanceDate TEXT NOT NULL DEFAULT '2026-01-01',
                OpeningExchangeRateId INTEGER NULL,
                OpeningExchangeRate NUMERIC NOT NULL DEFAULT 1,
                BaseOpeningBalance NUMERIC NOT NULL DEFAULT 0,
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

            CREATE UNIQUE INDEX UX_Cashboxes_Company_Code
            ON Cashboxes (CompanyId, Code) WHERE IsDeleted = 0;
            CREATE UNIQUE INDEX UX_Cashboxes_Company_Name
            ON Cashboxes (CompanyId, Name) WHERE IsDeleted = 0;

            CREATE TABLE CashMovementTypes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                Name TEXT NOT NULL COLLATE NOCASE,
                Direction INTEGER NOT NULL,
                PartnerEffect INTEGER NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                IsDefaultForSales INTEGER NOT NULL DEFAULT 0,
                IsDefaultForPurchase INTEGER NOT NULL DEFAULT 0,
                IsDefaultForSalesReturn INTEGER NOT NULL DEFAULT 0,
                IsDefaultForPurchaseReturn INTEGER NOT NULL DEFAULT 0,
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
                IsDeleted INTEGER NOT NULL,
                CONSTRAINT CK_CashMovementTypes_InvoiceDefaults
                    CHECK (
                        ((IsDefaultForSales = 0 AND
                          IsDefaultForPurchaseReturn = 0) OR
                         (IsActive = 1 AND Direction = 1 AND PartnerEffect = 2))
                        AND
                        ((IsDefaultForPurchase = 0 AND
                          IsDefaultForSalesReturn = 0) OR
                         (IsActive = 1 AND Direction = 2 AND PartnerEffect = 1)))
            );

            CREATE UNIQUE INDEX UX_CashMovementTypes_Company_Direction_Name
            ON CashMovementTypes (CompanyId, Direction, Name)
            WHERE IsDeleted = 0;

            CREATE UNIQUE INDEX IX_CashMovementTypes_Company_DefaultForSales
            ON CashMovementTypes (CompanyId, IsDefaultForSales)
            WHERE IsDeleted = 0 AND IsDefaultForSales = 1;

            CREATE UNIQUE INDEX IX_CashMovementTypes_Company_DefaultForPurchase
            ON CashMovementTypes (CompanyId, IsDefaultForPurchase)
            WHERE IsDeleted = 0 AND IsDefaultForPurchase = 1;

            CREATE UNIQUE INDEX IX_CashMovementTypes_Company_DefaultForSalesReturn
            ON CashMovementTypes (CompanyId, IsDefaultForSalesReturn)
            WHERE IsDeleted = 0 AND IsDefaultForSalesReturn = 1;

            CREATE UNIQUE INDEX IX_CashMovementTypes_Company_DefaultForPurchaseReturn
            ON CashMovementTypes (CompanyId, IsDefaultForPurchaseReturn)
            WHERE IsDeleted = 0 AND IsDefaultForPurchaseReturn = 1;

            CREATE TABLE DriverTrips (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                DriverId INTEGER NOT NULL,
                ActualDriverId INTEGER NULL,
                InvoiceId INTEGER NOT NULL,
                BusinessPartnerId INTEGER NOT NULL,
                InvoiceNumber TEXT NOT NULL,
                ExportInvoiceCode TEXT NULL,
                TripDate TEXT NOT NULL,
                Price NUMERIC NULL,
                Cost NUMERIC NULL,
                CostNotes TEXT NULL,
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

            CREATE TABLE CashVouchers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                InvoiceId INTEGER NULL,
                VoucherNumber TEXT NOT NULL COLLATE NOCASE,
                VoucherDate TEXT NOT NULL,
                Direction INTEGER NOT NULL,
                CashboxId INTEGER NULL,
                CashMovementTypeId INTEGER NULL,
                PartyType INTEGER NOT NULL,
                BusinessPartnerId INTEGER NULL,
                DriverId INTEGER NULL,
                DriverTripId INTEGER NULL,
                ExternalPartyName TEXT NULL,
                Amount NUMERIC NOT NULL,
                Currency INTEGER NOT NULL,
                ExchangeRateId INTEGER NULL,
                ExchangeRate NUMERIC NOT NULL DEFAULT 1,
                BaseAmount NUMERIC NOT NULL DEFAULT 0,
                ReferenceNumber TEXT NULL,
                Description TEXT NULL,
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
                IsDeleted INTEGER NOT NULL,
                CONSTRAINT CK_CashVouchers_PostingReferencesTogether CHECK (
                    (CashboxId IS NULL AND CashMovementTypeId IS NULL) OR
                    (CashboxId IS NOT NULL AND CashMovementTypeId IS NOT NULL))
            );

            CREATE INDEX IX_CashVouchers_Company_Number
            ON CashVouchers (CompanyId, VoucherNumber)
            WHERE IsDeleted = 0;

            CREATE TABLE InvoicePayments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                InvoiceId INTEGER NOT NULL,
                CashVoucherId INTEGER NOT NULL,
                InvoiceCurrency INTEGER NOT NULL,
                AppliedAmount NUMERIC NOT NULL,
                CashboxCurrency INTEGER NOT NULL,
                CashboxAmount NUMERIC NOT NULL,
                InvoiceToBaseRate NUMERIC NOT NULL,
                CashboxToBaseRate NUMERIC NOT NULL,
                AppliedBaseAmount NUMERIC NOT NULL,
                CashboxBaseAmount NUMERIC NOT NULL,
                RealizedExchangeDifference NUMERIC NOT NULL,
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

            CREATE TABLE BusinessPartnerMovements (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                BusinessPartnerId INTEGER NOT NULL,
                InvoiceId INTEGER NULL,
                CashVoucherId INTEGER NULL,
                MovementType INTEGER NOT NULL,
                MovementDate TEXT NOT NULL,
                Currency INTEGER NOT NULL,
                Debit NUMERIC NOT NULL,
                Credit NUMERIC NOT NULL,
                ExchangeRate NUMERIC NOT NULL DEFAULT 1,
                BaseDebit NUMERIC NOT NULL DEFAULT 0,
                BaseCredit NUMERIC NOT NULL DEFAULT 0,
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

            CREATE UNIQUE INDEX UX_PartnerMovements_Voucher
            ON BusinessPartnerMovements (CompanyId, CashVoucherId)
            WHERE CashVoucherId IS NOT NULL AND IsDeleted = 0;

            CREATE TRIGGER AdvanceCashboxRowVersion
            AFTER UPDATE ON Cashboxes
            BEGIN
                UPDATE Cashboxes SET RowVersion = randomblob(8)
                WHERE Id = NEW.Id;
            END;

            CREATE TRIGGER AdvanceCashMovementTypeRowVersion
            AFTER UPDATE ON CashMovementTypes
            BEGIN
                UPDATE CashMovementTypes SET RowVersion = randomblob(8)
                WHERE Id = NEW.Id;
            END;

            CREATE TRIGGER AdvanceCashVoucherRowVersion
            AFTER UPDATE ON CashVouchers
            BEGIN
                UPDATE CashVouchers SET RowVersion = randomblob(8)
                WHERE Id = NEW.Id;
            END;

            CREATE TRIGGER AdvanceDriverTripRowVersion
            AFTER UPDATE ON DriverTrips
            BEGIN
                UPDATE DriverTrips SET RowVersion = randomblob(8)
                WHERE Id = NEW.Id;
            END;
            """);
    }

    private static async Task SeedAsync(ApplicationDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Companies (
                Id, Name, Address, CommercialRegister, TaxNumber, ManagerName,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 'Company A', '', 'CR-A', 'TX-A', 'Manager',
                 'test', '2026-01-01', 'test', 0),
                (2, 'Company B', '', 'CR-B', 'TX-B', 'Manager',
                 'test', '2026-01-01', 'test', 0);

            INSERT INTO BusinessPartners (
                Id, CompanyId, Code, Name, Currency, CreditLimit, IsActive,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 'BP-1', 'Customer One', 1, 10000, 1,
                 'test', '2026-01-01', 'test', 0),
                (2, 1, 'BP-2', 'Supplier One', 1, 10000, 1,
                 'test', '2026-01-01', 'test', 0),
                (3, 2, 'BP-3', 'Other Company Partner', 1, 10000, 1,
                 'test', '2026-01-01', 'test', 0),
                (4, 1, 'BP-4', 'Inactive Partner', 1, 10000, 0,
                 'test', '2026-01-01', 'test', 0);

            INSERT INTO Drivers (
                Id, CompanyId, Code, Name, LicenseNumber, IsActive,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 'DRV-1', 'Driver One', 'LIC-1', 1,
                 'test', '2026-01-01', 'test', 0),
                (2, 1, 'DRV-2', 'Driver Two', 'LIC-2', 1,
                 'test', '2026-01-01', 'test', 0),
                (3, 2, 'DRV-3', 'Other Company Driver', 'LIC-3', 1,
                 'test', '2026-01-01', 'test', 0);

            INSERT INTO Invoices (Id, CompanyId, InvoiceNumber, IsDeleted)
            VALUES
                (1, 1, 'INV-1', 0),
                (2, 1, 'INV-2', 0),
                (3, 2, 'INV-3', 0);

            INSERT INTO Cashboxes (
                Id, CompanyId, Code, Name, Currency, OpeningBalance, IsActive,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 'MAIN', 'Main Cashbox', 1, 1000, 1,
                 'test', '2026-01-01', 'test', 0),
                (2, 1, 'SECOND', 'Second Cashbox', 1, 500, 1,
                 'test', '2026-01-01', 'test', 0),
                (3, 1, 'INACTIVE', 'Inactive Cashbox', 1, 500, 0,
                 'test', '2026-01-01', 'test', 0),
                (4, 2, 'MAIN', 'Other Company Cashbox', 1, 1000, 1,
                 'test', '2026-01-01', 'test', 0);

            INSERT INTO CashMovementTypes (
                Id, CompanyId, Name, Direction, PartnerEffect, IsActive,
                IsDefaultForSales, IsDefaultForPurchase,
                IsDefaultForSalesReturn, IsDefaultForPurchaseReturn,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 'Customer Collection', 1, 2, 1, 1, 0, 0, 0,
                 'test', '2026-01-01', 'test', 0),
                (2, 1, 'Supplier Payment', 2, 1, 1, 0, 1, 0, 0,
                 'test', '2026-01-01', 'test', 0),
                (3, 1, 'Other Receipt', 1, 0, 1, 0, 0, 0, 0,
                 'test', '2026-01-01', 'test', 0),
                (4, 1, 'Driver Advance', 2, 0, 1, 0, 0, 0, 0,
                 'test', '2026-01-01', 'test', 0),
                (5, 1, 'Inactive Payment', 2, 0, 0, 0, 0, 0, 0,
                 'test', '2026-01-01', 'test', 0),
                (6, 2, 'Other Receipt', 1, 0, 1, 0, 0, 0, 0,
                 'test', '2026-01-01', 'test', 0),
                (7, 1, 'Supplier Refund', 1, 2, 1, 0, 0, 0, 1,
                 'test', '2026-01-01', 'test', 0),
                (8, 1, 'Customer Refund', 2, 1, 1, 0, 0, 1, 0,
                 'test', '2026-01-01', 'test', 0);

            INSERT INTO DriverTrips (
                Id, CompanyId, DriverId, InvoiceId, BusinessPartnerId,
                InvoiceNumber, TripDate, Cost, CostNotes,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 1, 1, 1, 'INV-1', '2026-07-20', NULL, NULL,
                 'test', '2026-07-20', 'test', 0),
                (2, 1, 2, 2, 1, 'INV-2', '2026-07-21', 40, 'Fuel',
                 'test', '2026-07-21', 'test', 0),
                (3, 2, 3, 3, 3, 'INV-3', '2026-07-22', NULL, NULL,
                 'test', '2026-07-22', 'test', 0);

            INSERT INTO PartnerOpeningBalances (
                Id, CompanyId, BusinessPartnerId, DocumentNumber, DocumentDate,
                Currency, BalanceType, Amount, Notes,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 1, 'OPEN-1', '2026-07-01', 1, 1, 200, 'Opening',
                 'test', '2026-07-01', 'test', 0);

            INSERT INTO BusinessPartnerMovements (
                Id, CompanyId, BusinessPartnerId, InvoiceId, CashVoucherId,
                MovementType, MovementDate, Currency, Debit, Credit, Description,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES
                (1, 1, 1, 1, NULL, 1, '2026-07-10', 1, 100, 0,
                 'Invoice movement', 'test', '2026-07-10', 'test', 0);
            """);
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
