using System.Diagnostics.Metrics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Features.Dashboard;
using MiniErp.Application.Features.ProfitabilityReports;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Dashboard;
using MiniErp.Infrastructure.Services.ProfitabilityReports;

namespace MiniErp.Tests.Dashboard;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task Get_UsesCurrentFiscalYearAndReturnsZeroMonths()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.FiscalYearId);
        Assert.Equal(new DateOnly(2026, 1, 1), result.Value.FromDate);
        Assert.Equal(new DateOnly(2026, 12, 31), result.Value.ToDate);
        Assert.Equal(CurrencyCode.EGP, result.Value.BaseCurrency);
        Assert.Equal(12, result.Value.MonthlyActivity.Count);
        Assert.All(result.Value.MonthlyActivity, month =>
        {
            Assert.Equal(0m, month.Sales);
            Assert.Equal(0m, month.Purchases);
        });
        Assert.True(result.Value.Accounting.IsReady);
        Assert.Empty(result.Value.Alerts);
    }

    [Fact]
    public async Task Get_RejectsRangeOutsideCurrentFiscalYear()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest(
                FromDate: new DateOnly(2025, 12, 31),
                ToDate: new DateOnly(2026, 1, 2)));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error =>
            error.Code == "Dashboard.InvalidDateRange");
    }

    [Fact]
    public async Task Get_FailsWhenCompanyHasNoCurrentFiscalYear()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE FiscalYears SET IsCurrent = 0 WHERE CompanyId = 1;");

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest());

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error =>
            error.Code == "Dashboard.FiscalYearNotFound");
    }

    [Fact]
    public async Task Profitability_IncludesCurrentYearReturnLinkedToPriorYearSale()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedPriorFiscalYearAsync();
        await database.SeedInvoicesAsync();
        await database.SeedPriorYearInvoiceAsync();
        await database.SeedPriorYearDraftVoucherAndPendingMovementAsync();
        await database.SeedCrossYearLinkedReturnLinesAsync();

        var report = await new ProfitabilityReportService(
                database.Context,
                new TestCurrentCompanyContext(1))
            .GetInvoicesAsync(
                new PaginationRequest
                {
                    PageNumber = 1,
                    PageSize = 10
                },
                new ProfitabilityReportFilterRequest(
                    FromDate: new DateOnly(2026, 1, 1),
                    ToDate: new DateOnly(2026, 12, 31),
                    FiscalYearId: 1));

        Assert.True(report.IsSuccess);
        Assert.Equal(10m, report.Value.Summary.ReturnRevenue);
        Assert.Equal(-10m, report.Value.Summary.NetRevenue);
        Assert.Equal(0m, report.Value.Summary.SalesRevenue);
    }

    [Fact]
    public async Task Get_CarriesPriorInventorySnapshotWithoutCurrentYearMovement()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedPriorFiscalYearAsync();
        await database.SeedInvoicesAsync();
        await database.SeedPriorYearDraftVoucherAndPendingMovementAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            "DELETE FROM ItemMovements WHERE ReferenceNumber = 'CURRENT-OPEN';");

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value.Inventory.CurrentInventoryValue);
        Assert.Equal(1, result.Value.Inventory.ItemsWithStockCount);
        Assert.Equal(0, result.Value.Inventory.PendingCostMovementCount);
    }

    [Fact]
    public async Task Get_ExcludesPriorYearInvoicesCashAndAlerts()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedPriorFiscalYearAsync();
        await database.SeedInvoicesAsync();
        await database.SeedPriorYearInvoiceAsync();
        await database.SeedCashboxWithoutVouchersAsync(openingBalance: 0m);
        await database.SeedCashboxLedgerEntryAsync(
            amount: 75m,
            isOpening: false);
        await database.SeedCashboxLedgerEntryAsync(
            amount: 125m,
            isOpening: false,
            fiscalYearId: 2,
            entryDate: "2025-09-02");
        await database.SeedPriorYearDraftVoucherAndPendingMovementAsync();

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.FiscalYearId);
        Assert.Equal(100m, result.Value.Sales.Total);
        Assert.Equal(1, database.ProfitabilityService.LastFilters?.FiscalYearId);
        Assert.Equal(4, result.Value.Counts.InvoiceCount);
        Assert.Equal(75m, Assert.Single(result.Value.CashBalances).CurrentBalance);
        Assert.Equal(25m, result.Value.Inventory.CurrentInventoryValue);
        Assert.Equal(1, result.Value.Inventory.ItemsWithStockCount);
        Assert.Equal(0, result.Value.Inventory.PendingCostMovementCount);
        Assert.DoesNotContain(result.Value.Alerts, alert =>
            alert.Code is "PendingInventoryCosts" or "DraftCashVouchers");

        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE FiscalYears SET IsCurrent = CASE WHEN Id = 2 THEN 1 ELSE 0 END WHERE CompanyId = 1;");
        var priorYearResult = await database.Service.GetAsync(
            new DashboardFilterRequest());

        Assert.True(priorYearResult.IsSuccess);
        Assert.Equal(2, priorYearResult.Value.FiscalYearId);
        Assert.Equal(900m, priorYearResult.Value.Sales.Total);
        Assert.Equal(1, priorYearResult.Value.Counts.InvoiceCount);
        Assert.Equal(125m,
            Assert.Single(priorYearResult.Value.CashBalances).CurrentBalance);
        Assert.Equal(100m,
            priorYearResult.Value.Inventory.CurrentInventoryValue);
        Assert.Equal(1,
            priorYearResult.Value.Inventory.PendingCostMovementCount);
        Assert.Contains(priorYearResult.Value.Alerts, alert =>
            alert.Code == "PendingInventoryCosts" && alert.Count == 1);
        Assert.Contains(priorYearResult.Value.Alerts, alert =>
            alert.Code == "DraftCashVouchers" && alert.Count == 1);
    }

    [Fact]
    public async Task Get_ReturnsZeroBalanceForCashboxWithoutVouchers()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedCashboxWithoutVouchersAsync(openingBalance: 0m);

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest());

        Assert.True(result.IsSuccess);
        var cashBalance = Assert.Single(result.Value.CashBalances);
        Assert.Equal(CurrencyCode.EGP, cashBalance.Currency);
        Assert.Equal(1, cashBalance.CashboxCount);
        Assert.Equal(0m, cashBalance.CurrentBalance);
    }

    [Fact]
    public async Task Get_IncludesOpeningBalanceForCashboxWithoutVouchers()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedCashboxWithoutVouchersAsync(openingBalance: 250m);
        await database.SeedCashboxLedgerEntryAsync(
            amount: 250m,
            isOpening: true);

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest());

        Assert.True(result.IsSuccess);
        var cashBalance = Assert.Single(result.Value.CashBalances);
        Assert.Equal(CurrencyCode.EGP, cashBalance.Currency);
        Assert.Equal(1, cashBalance.CashboxCount);
        Assert.Equal(250m, cashBalance.CurrentBalance);
    }

    [Fact]
    public async Task Get_IgnoresOperationalCashboxOpeningBalanceWithoutJournal()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedCashboxWithoutVouchersAsync(openingBalance: 250m);

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest());

        Assert.True(result.IsSuccess);
        var cashBalance = Assert.Single(result.Value.CashBalances);
        Assert.Equal(0m, cashBalance.CurrentBalance);
    }

    [Fact]
    public async Task Get_IncludesManualCashboxJournalLinesWithoutVoucher()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedCashboxWithoutVouchersAsync(openingBalance: 0m);
        await database.SeedCashboxLedgerEntryAsync(
            amount: 75m,
            isOpening: false);

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest());

        Assert.True(result.IsSuccess);
        var cashBalance = Assert.Single(result.Value.CashBalances);
        Assert.Equal(75m, cashBalance.CurrentBalance);
    }

    [Fact]
    public async Task Get_CalculatesBaseCurrencyTotalsReturnsAndOutstanding()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedInvoicesAsync();

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest(
                FromDate: new DateOnly(2026, 9, 1),
                ToDate: new DateOnly(2026, 9, 30)));

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value.Sales.Total);
        Assert.Equal(10m, result.Value.Sales.Returns);
        Assert.Equal(90m, result.Value.Sales.Net);
        Assert.Equal(80m, result.Value.Sales.Outstanding);
        Assert.Equal(50m, result.Value.Purchases.Total);
        Assert.Equal(5m, result.Value.Purchases.Returns);
        Assert.Equal(45m, result.Value.Purchases.Net);
        Assert.Equal(40m, result.Value.Purchases.Outstanding);
        Assert.Equal(4, result.Value.Counts.InvoiceCount);
        Assert.Equal(2, result.Value.Counts.BusinessPartnerCount);
        Assert.Equal(2, result.Value.InvoiceStatus.PartiallyPaidCount);
        Assert.Equal(2, result.Value.InvoiceStatus.OverdueCount);
        Assert.Equal(120m, result.Value.InvoiceStatus.OverdueAmount);
        Assert.Equal(90m, result.Value.MonthlyActivity.Single().Sales);
        Assert.Equal(45m, result.Value.MonthlyActivity.Single().Purchases);
        Assert.Contains(result.Value.Alerts, alert =>
            alert.Code == "OverdueInvoices" && alert.Count == 2);
    }

    [Fact]
    public async Task Reports_EmitDurationAndWorkloadMetrics()
    {
        var observed = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "MiniErp.Reporting")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>(
            (instrument, _, _, _) => observed.Add(instrument.Name));
        listener.SetMeasurementEventCallback<long>(
            (instrument, _, _, _) => observed.Add(instrument.Name));
        listener.Start();

        await using var database = await TestDatabase.CreateAsync();
        var dashboard = await database.Service.GetAsync(
            new DashboardFilterRequest());
        var profitability = await new ProfitabilityReportService(
                database.Context,
                new TestCurrentCompanyContext(1))
            .GetInvoicesAsync(
                new PaginationRequest
                {
                    PageNumber = 1,
                    PageSize = 10
                },
                new ProfitabilityReportFilterRequest(),
                CancellationToken.None);

        Assert.True(dashboard.IsSuccess);
        Assert.True(profitability.IsSuccess);
        Assert.Contains("mini_erp.dashboard.duration", observed);
        Assert.Contains("mini_erp.profitability.duration", observed);
        Assert.Contains("mini_erp.profitability.loaded_lines", observed);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context,
            DashboardService service,
            EmptyProfitabilityService profitabilityService)
        {
            this.connection = connection;
            Context = context;
            Service = service;
            ProfitabilityService = profitabilityService;
        }

        public ApplicationDbContext Context { get; }

        public DashboardService Service { get; }

        public EmptyProfitabilityService ProfitabilityService { get; }

        public async Task SeedCashboxWithoutVouchersAsync(decimal openingBalance)
        {
            await Context.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO Cashboxes (
                    CompanyId, Code, Name, Currency, OpeningBalance,
                    OpeningBalanceDate, OpeningExchangeRateId,
                    OpeningExchangeRate, BaseOpeningBalance, IsActive,
                    Notes, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES (
                    1, 'CB-EMPTY', 'Empty Dashboard Cashbox', 1,
                    {openingBalance}, '2026-01-01', NULL, 1,
                    {openingBalance}, 1, NULL, randomblob(8), 'test',
                    '2026-09-01', 'test', 0);
                """);
            Context.ChangeTracker.Clear();
        }

        public async Task SeedCashboxLedgerEntryAsync(
            decimal amount,
            bool isOpening,
            int fiscalYearId = 1,
            string? entryDate = null)
        {
            var cashbox = await Context.Cashboxes
                .AsNoTracking()
                .SingleAsync(entity => entity.Code == "CB-EMPTY");
            var entryNumber = isOpening
                ? $"JE-CASHBOX-OPEN-{fiscalYearId}"
                : $"JE-CASHBOX-MANUAL-{fiscalYearId}";
            entryDate ??= isOpening
                ? "2026-01-01"
                : "2026-09-02";
            var entryType = isOpening ? 4 : 1;
            int? sourceType = isOpening
                ? (int)JournalEntrySourceType.CashboxOpeningBalance
                : null;
            int? sourceId = isOpening ? cashbox.Id : null;

            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO JournalEntries (
                    CompanyId, FiscalYearId, EntryNumber, EntryDate,
                    Description, EntryType, SourceType, SourceId,
                    SourceNumber, Status, PostedOn, RowVersion,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    1, {fiscalYearId}, {entryNumber}, {entryDate}, {entryNumber},
                    {entryType}, {sourceType}, {sourceId}, NULL, 1,
                    {entryDate}, randomblob(8), 'test', {entryDate},
                    'test', 0);
                """);
            var journalEntryId = await Context.JournalEntries
                .AsNoTracking()
                .Where(entry => entry.EntryNumber == entryNumber)
                .Select(entry => entry.Id)
                .SingleAsync();
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO JournalEntryLines (
                    CompanyId, JournalEntryId, AccountId, PartyType, PartyId,
                    Description, Debit, Credit, Currency, ExchangeRate,
                    TransactionDebit, TransactionCredit,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    1, {journalEntryId}, 1, 5, {cashbox.Id}, NULL,
                    CAST({amount} AS NUMERIC), 0, 1, 1,
                    CAST({amount} AS NUMERIC), 0,
                    'test', {entryDate}, 'test', 0),
                    (1, {journalEntryId}, 2, NULL, NULL, NULL,
                    0, CAST({amount} AS NUMERIC), 1, 1, 0,
                    CAST({amount} AS NUMERIC), 'test', {entryDate},
                    'test', 0);
                """);
            Context.ChangeTracker.Clear();
        }

        public async Task SeedInvoicesAsync()
        {
            var auditDate = new DateTime(2026, 9, 1);
            var partner = new BusinessPartner
            {
                CompanyId = 1,
                Code = "BP-1",
                Name = "Dashboard Partner",
                Currency = CurrencyCode.EGP,
                IsActive = true,
                CreatedById = "test",
                CreatedByPc = "test",
                CreatedOn = auditDate
            };
            var inactivePartner = new BusinessPartner
            {
                CompanyId = 1,
                Code = "BP-2",
                Name = "Inactive Dashboard Partner",
                Currency = CurrencyCode.EGP,
                IsActive = false,
                CreatedById = "test",
                CreatedByPc = "test",
                CreatedOn = auditDate
            };
            var store = new Store
            {
                CompanyId = 1,
                Code = "ST-1",
                Name = "Dashboard Store",
                IsActive = true,
                CreatedById = "test",
                CreatedByPc = "test",
                CreatedOn = auditDate
            };
            Context.AddRange(partner, inactivePartner, store);
            await Context.SaveChangesAsync();

            await Context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Invoices (
                    Id, CompanyId, FiscalYearId, InvoiceNumber, InvoiceType, ContentType,
                    PaymentTerm, InvoiceDate, DueDate, BusinessPartnerId,
                    StoreId, Currency, ExchangeRate, UsesExternalDriver,
                    DiscountAmount, WBWeight, WBScaleDifference, WBDiscount,
                    WBTotal, PaidAmount, Total, BaseSubtotal,
                    BaseDiscountAmount, BaseTotal,
                    BasePaidAmountAtInvoiceRate, LastModifiedAt, RowVersion,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 1, 'S-1', 1, 1, 1, '2026-09-03', '2026-09-03',
                     1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 20, 100, 100, 0, 100,
                     20, '2026-09-03', randomblob(8), 'test', '2026-09-03',
                     'test', 0),
                    (2, 1, 1, 'SR-1', 3, 1, 1, '2026-09-03', NULL,
                     1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 10, 10, 0, 10,
                     0, '2026-09-03', randomblob(8), 'test', '2026-09-03',
                     'test', 0),
                    (3, 1, 1, 'P-1', 2, 1, 1, '2026-09-03', '2026-09-03',
                     1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 10, 50, 50, 0, 50,
                     10, '2026-09-03', randomblob(8), 'test', '2026-09-03',
                     'test', 0),
                    (4, 1, 1, 'PR-1', 4, 1, 1, '2026-09-03', NULL,
                     1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 5, 5, 0, 5,
                     0, '2026-09-03', randomblob(8), 'test', '2026-09-03',
                     'test', 0);
                """);
            Context.ChangeTracker.Clear();
        }

        public async Task SeedPriorFiscalYearAsync()
        {
            await Context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO FiscalYears (
                    Id, CompanyId, Name, StartDate, EndDate, Status,
                    IsCurrent, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES (
                    2, 1, '2025', '2025-01-01', '2025-12-31', 1, 0,
                    randomblob(8), 'test', '2025-01-01', 'test', 0);
                """);
        }

        public async Task SeedPriorYearInvoiceAsync()
        {
            await Context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Invoices (
                    Id, CompanyId, FiscalYearId, InvoiceNumber,
                    InvoiceType, ContentType, PaymentTerm, InvoiceDate,
                    DueDate, BusinessPartnerId, StoreId, Currency,
                    ExchangeRate, UsesExternalDriver, DiscountAmount,
                    WBWeight, WBScaleDifference, WBDiscount, WBTotal,
                    PaidAmount, Total, BaseSubtotal, BaseDiscountAmount,
                    BaseTotal, BasePaidAmountAtInvoiceRate, LastModifiedAt,
                    RowVersion, CreatedById, CreatedOn, CreatedByPc,
                    IsDeleted)
                VALUES (
                    5, 1, 2, 'S-2025', 1, 1, 1, '2025-09-03',
                    '2025-09-03', 1, 1, 1, 1, 0, 0, 0, 0, 0, 0,
                    0, 900, 900, 0, 900, 0, '2025-09-03',
                    randomblob(8), 'test', '2025-09-03', 'test', 0);
                """);
            Context.ChangeTracker.Clear();
        }

        public async Task SeedPriorYearDraftVoucherAndPendingMovementAsync()
        {
            await Context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO CashVouchers (
                    CompanyId, FiscalYearId, VoucherNumber, VoucherDate,
                    Direction, PartyType, Amount, Currency, ExchangeRate,
                    BaseAmount, IsPosted, LastModifiedAt, RowVersion,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    1, 2, 'CV-2025', '2025-09-03', 1, 1, 10, 1, 1,
                    10, 0, '2025-09-03', randomblob(8), 'test',
                    '2025-09-03', 'test', 0);
                """);

            var unit = new ItemUnit
            {
                CompanyId = 1,
                Name = "Unit",
                CreatedById = "test",
                CreatedByPc = "test"
            };
            var item = new Item
            {
                CompanyId = 1,
                Code = "ITEM-FY",
                Name = "Fiscal Item",
                ItemUnit = unit,
                CreatedById = "test",
                CreatedByPc = "test"
            };
            Context.Items.Add(item);
            await Context.SaveChangesAsync();

            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO ItemMovements (
                    CompanyId, FiscalYearId, StoreId, ItemId,
                    MovementType, ReferenceId, ReferenceNumber,
                    MovementDate, QuantityIn, QuantityOut, CostStatus,
                    PendingCostQuantity, UnitCost, TotalCost,
                    QuantityAfter, AverageCostAfter, InventoryValueAfter,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 2, 1, {item.Id}, 5, 1, 'OLD-OPEN',
                     '2025-09-03', 5, 0, 3, 5, NULL, 100,
                     5, 20, 100, 'test', '2025-09-03', 'test', 0),
                    (1, 1, 1, {item.Id}, 5, 2, 'CURRENT-OPEN',
                     '2026-01-01', 1, 0, 1, 0, 25, 25,
                     1, 25, 25, 'test', '2026-01-01', 'test', 0);
                """);
            Context.ChangeTracker.Clear();
        }

        public async Task SeedCrossYearLinkedReturnLinesAsync()
        {
            await Context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO InvoiceLines (
                    Id, CompanyId, InvoiceId, ItemId, ItemUnitId,
                    SourceInvoiceLineId, Count, Weight, Quantity,
                    Price, Total, BaseUnitPrice, BaseTotal,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (100, 1, 5, 1, 1, NULL, 1, 1, 1,
                     10, 10, 10, 10, 'test', '2025-09-03', 'test', 0),
                    (101, 1, 2, 1, 1, 100, 1, 1, 1,
                     10, 10, 10, 10, 'test', '2026-09-03', 'test', 0);
                """);
            Context.ChangeTracker.Clear();
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Companies (
                    Id, Name, Address, CommercialRegister, TaxNumber,
                    ManagerName, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES (
                    1, 'Dashboard Test Company', 'Test Address',
                    'CR-DASHBOARD', 'TAX-DASHBOARD', 'Test Manager',
                    randomblob(8), 'test', '2026-01-01', 'test', 0);

                INSERT INTO CompanySettings (
                    CompanyId, BaseCurrency, StockBalanceCheckMode)
                VALUES (1, 1, 1);

                INSERT INTO FiscalYears (
                    Id, CompanyId, Name, StartDate, EndDate, Status,
                    IsCurrent, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES (
                    1, 1, '2026', '2026-01-01', '2026-12-31', 1, 1,
                    randomblob(8), 'test', '2026-01-01', 'test', 0);

                INSERT INTO Accounts (
                    Id, CompanyId, Code, Name, ParentAccountId,
                    AccountType, NormalBalance, IsPosting, IsActive,
                    RowVersion, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, '1000', 'Cashbox account', NULL, 1, 1, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0),
                    (2, 1, '3100', 'Opening equity', NULL, 3, 2, 1, 1,
                     randomblob(8), 'test', '2026-01-01', 'test', 0);
                """);

            var profitabilityService = new EmptyProfitabilityService();
            var service = new DashboardService(
                context,
                new TestCurrentCompanyContext(1),
                profitabilityService,
                new ReadyAccountingService(),
                new FixedTimeProvider(
                    new DateTimeOffset(
                        2026,
                        9,
                        4,
                        12,
                        0,
                        0,
                        TimeSpan.Zero)));
            return new TestDatabase(
                connection,
                context,
                service,
                profitabilityService);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class EmptyProfitabilityService
        : IProfitabilityReportService
    {
        public ProfitabilityReportFilterRequest? LastFilters { get; private set; }

        public Task<Result<InvoiceProfitabilityListResponse>> GetInvoicesAsync(
            PaginationRequest pagination,
            ProfitabilityReportFilterRequest filters,
            CancellationToken cancellationToken = default)
        {
            LastFilters = filters;
            return Task.FromResult(Result<InvoiceProfitabilityListResponse>.Success(
                new InvoiceProfitabilityListResponse(
                    IncludeReturns: true,
                    BaseCurrency: CurrencyCode.EGP,
                    FromDate: filters.FromDate,
                    ToDate: filters.ToDate,
                    Invoices: [],
                    PageNumber: pagination.PageNumber,
                    PageSize: pagination.PageSize,
                    TotalCount: 0,
                    TotalPages: 0,
                    Summary: new ProfitabilityReportSummaryResponse(
                        SalesRevenue: 0m,
                        SalesCost: 0m,
                        ReturnRevenue: 0m,
                        ReturnCost: 0m,
                        NetRevenue: 0m,
                        RecognizedCost: 0m,
                        GrossProfit: 0m,
                        GrossMarginPercentage: null,
                        FinalizedNetRevenue: 0m,
                        FinalizedCost: 0m,
                        FinalizedGrossProfit: 0m,
                        FinalizedGrossMarginPercentage: null,
                        PendingRevenue: 0m,
                        PendingCostQuantity: 0m,
                        InvoiceCount: 0,
                        ItemCount: 0,
                        LineCount: 0,
                        PendingInvoiceCount: 0,
                        PendingLineCount: 0))));
        }

        public Task<Result<InvoiceProfitabilityResponse>>
            GetInvoiceDetailsAsync(
                int invoiceId,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<ItemProfitabilityListResponse>> GetItemsAsync(
            PaginationRequest pagination,
            ProfitabilityReportFilterRequest filters,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ReadyAccountingService
        : IAccountingReadinessService
    {
        public Task<Result<AccountingReadinessResponse>> GetAsync(
            int fiscalYearId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AccountingReadinessResponse>.Success(
                new AccountingReadinessResponse(
                    FiscalYearId: fiscalYearId,
                    FiscalYearName: "2026",
                    StartDate: new DateOnly(2026, 1, 1),
                    EndDate: new DateOnly(2026, 12, 31),
                    IsReady: true,
                    TotalSources: 0,
                    PostedSources: 0,
                    MissingJournalSources: 0,
                    OrphanAutomaticJournals: 0,
                    DuplicateAutomaticJournals: 0,
                    UnbalancedAutomaticJournals: 0,
                    PendingInventoryCosts: 0,
                    MissingOrInvalidMappings: 0,
                    DeferredPayrollSources: 0,
                    Sources: [],
                    Issues: [])));

        public Task<Result<AccountingBackfillResponse>> BackfillAsync(
            int fiscalYearId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
